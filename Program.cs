using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

class Program
{
    const int PORT = 8080;
    const string WEB_ROOT = "WebRoot";
    static readonly string[] ALLOWED_EXTENSIONS = { 
        ".html", ".css", ".js", ".txt", ".json", ".xml", 
        ".jpg", ".jpeg", ".png", ".gif", ".ico", ".svg",
        ".pdf", ".zip"
    };
    
    static readonly Dictionary<string, string> MIME_TYPES = new()
    {
        [".html"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".txt"] = "text/plain",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".ico"] = "image/x-icon",
        [".svg"] = "image/svg+xml",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip"
    };

    static readonly Dictionary<string, byte[]> _cache = new();
    static readonly object _cacheLock = new object();
    static CancellationTokenSource _cancellationTokenSource = new();

    static async Task Main(string[] args)
    {
        // Ensure WebRoot directory exists
        if (!Directory.Exists(WEB_ROOT))
        {
            Directory.CreateDirectory(WEB_ROOT);
            Console.WriteLine($"Created {WEB_ROOT} directory");
        }

        TcpListener listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        
        Console.WriteLine($"Enhanced C# Web Server running on http://localhost:{PORT}");
        Console.WriteLine($"Serving files from: {Path.GetFullPath(WEB_ROOT)}");
        Console.WriteLine("Supported file types: " + string.Join(", ", ALLOWED_EXTENSIONS));
        Console.WriteLine("Press Ctrl+C to stop the server...\n");

        // Handle graceful shutdown
        Console.CancelKeyPress += (sender, e) => {
            e.Cancel = true;
            _cancellationTokenSource.Cancel();
            listener.Stop();
            Console.WriteLine("\nShutting down server...");
        };

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (listener.Pending())
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client), _cancellationTokenSource.Token);
                }
                await Task.Delay(10); // Small delay to prevent busy waiting
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected when listener is stopped
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Server error: {ex.Message}");
        }
        finally
        {
            listener?.Stop();
            Console.WriteLine("Server stopped.");
        }
    }

    static async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            try
            {
                // Set timeouts
                client.ReceiveTimeout = 5000; // 5 seconds
                client.SendTimeout = 5000;

                // Read request with proper buffering
                var requestBuilder = new StringBuilder();
                byte[] buffer = new byte[1024];
                int totalBytesRead = 0;
                
                do
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    
                    totalBytesRead += bytesRead;
                    requestBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                    
                    // Check if we have complete headers (empty line)
                    if (requestBuilder.ToString().Contains("\r\n\r\n"))
                        break;
                        
                } while (totalBytesRead < 8192 && stream.DataAvailable); // Limit request size

                string request = requestBuilder.ToString();
                if (string.IsNullOrEmpty(request))
                {
                    SendErrorResponse(stream, 400, "Bad Request");
                    return;
                }

                // Parse HTTP request
                var httpRequest = ParseHttpRequest(request);
                if (httpRequest == null)
                {
                    SendErrorResponse(stream, 400, "Bad Request");
                    return;
                }

                LogRequest(httpRequest, client.Client.RemoteEndPoint?.ToString() ?? "unknown");

                // Handle different HTTP methods
                switch (httpRequest.Method.ToUpper())
                {
                    case "GET":
                        await HandleGetRequest(stream, httpRequest);
                        break;
                    case "HEAD":
                        await HandleHeadRequest(stream, httpRequest);
                        break;
                    case "OPTIONS":
                        HandleOptionsRequest(stream);
                        break;
                    default:
                        SendErrorResponse(stream, 405, "Method Not Allowed", 
                            new Dictionary<string, string> { ["Allow"] = "GET, HEAD, OPTIONS" });
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                try
                {
                    SendErrorResponse(stream, 500, "Internal Server Error");
                }
                catch
                {
                    // Ignore errors when sending error response
                }
            }
        }
    }

    static HttpRequest? ParseHttpRequest(string request)
    {
        try
        {
            string[] lines = request.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0) return null;

            // Parse request line
            string[] requestParts = lines[0].Split(' ');
            if (requestParts.Length < 3) return null;

            var httpRequest = new HttpRequest
            {
                Method = requestParts[0],
                RawUrl = requestParts[1],
                Version = requestParts[2],
                Headers = new Dictionary<string, string>()
            };

            // Parse URL and query string
            string[] urlParts = httpRequest.RawUrl.Split('?', 2);
            httpRequest.Path = HttpUtility.UrlDecode(urlParts[0]);
            
            if (urlParts.Length > 1)
            {
                httpRequest.QueryString = ParseQueryString(urlParts[1]);
            }

            // Parse headers
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i])) break;
                
                int colonIndex = lines[i].IndexOf(':');
                if (colonIndex > 0)
                {
                    string headerName = lines[i].Substring(0, colonIndex).Trim().ToLower();
                    string headerValue = lines[i].Substring(colonIndex + 1).Trim();
                    httpRequest.Headers[headerName] = headerValue;
                }
            }

            return httpRequest;
        }
        catch
        {
            return null;
        }
    }

    static Dictionary<string, string> ParseQueryString(string queryString)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(queryString)) return result;

        foreach (string pair in queryString.Split('&'))
        {
            string[] keyValue = pair.Split('=', 2);
            string key = HttpUtility.UrlDecode(keyValue[0]);
            string value = keyValue.Length > 1 ? HttpUtility.UrlDecode(keyValue[1]) : "";
            result[key] = value;
        }

        return result;
    }

    static async Task HandleGetRequest(NetworkStream stream, HttpRequest request)
    {
        string filePath = GetSafeFilePath(request.Path);
        if (filePath == null)
        {
            SendErrorResponse(stream, 403, "Forbidden");
            return;
        }

        // Handle directory requests
        if (Directory.Exists(filePath))
        {
            filePath = Path.Combine(filePath, "index.html");
        }

        // Current problematic code in HandleGetRequest:
if (!File.Exists(filePath))
{
    // Try common variations
    string[] alternatives = { filePath + ".html", filePath + "/index.html" };
    bool found = false;
    
    foreach (string alt in alternatives)
    {
        if (File.Exists(alt))
        {
            filePath = alt;
            found = true;
            break;
        }
    }
    
    if (!found)
    {
        SendErrorResponse(stream, 404, "Not Found");
        return;
    }
}

string extension = Path.GetExtension(filePath).ToLower();
if (!ALLOWED_EXTENSIONS.Contains(extension))
{
    SendErrorResponse(stream, 403, "Forbidden");
    return;
}

string requestedExtension = Path.GetExtension(request.Path).ToLower();

if (!string.IsNullOrEmpty(requestedExtension) && !ALLOWED_EXTENSIONS.Contains(requestedExtension))
{
    SendErrorResponse(stream, 403, "Forbidden");
    return;
}

if (!File.Exists(filePath))
{
    // Try common variations only if original request had no extension or was valid
    string[] alternatives = { filePath + ".html", filePath + "/index.html" };
    bool found = false;
    
    foreach (string alt in alternatives)
    {
        if (File.Exists(alt))
        {
            string altExtension = Path.GetExtension(alt).ToLower();
            if (ALLOWED_EXTENSIONS.Contains(altExtension))
            {
                filePath = alt;
                found = true;
                break;
            }
        }
    }
    
    if (!found)
    {
        SendErrorResponse(stream, 404, "Not Found");
        return;
    }
}

// Final validation for the actual file being served
string finalExtension = Path.GetExtension(filePath).ToLower();
if (!ALLOWED_EXTENSIONS.Contains(finalExtension))
{
    SendErrorResponse(stream, 403, "Forbidden");
    return;
}

        try
        {
            // Check if file is cached and not modified
            byte[] content = await GetFileContentAsync(filePath);
            string mimeType = MIME_TYPES.GetValueOrDefault(extension, "application/octet-stream");
            
            var headers = new Dictionary<string, string>
            {
                ["Last-Modified"] = File.GetLastWriteTimeUtc(filePath).ToString("R"),
                ["Cache-Control"] = GetCacheControl(extension),
                ["ETag"] = $"\"{content.Length}-{File.GetLastWriteTimeUtc(filePath).Ticks}\""
            };

            // Handle conditional requests
            if (request.Headers.TryGetValue("if-none-match", out string? etag) && 
                etag == headers["ETag"])
            {
                SendResponse(stream, 304, "Not Modified", mimeType, Array.Empty<byte>(), headers);
                return;
            }

            SendResponse(stream, 200, "OK", mimeType, content, headers);
        }
        catch (UnauthorizedAccessException)
        {
            SendErrorResponse(stream, 403, "Forbidden");
        }
        catch (FileNotFoundException)
        {
            SendErrorResponse(stream, 404, "Not Found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error serving file {filePath}: {ex.Message}");
            SendErrorResponse(stream, 500, "Internal Server Error");
        }
    }

    static async Task HandleHeadRequest(NetworkStream stream, HttpRequest request)
    {
        string filePath = GetSafeFilePath(request.Path);
        if (filePath == null)
        {
            SendErrorResponse(stream, 403, "Forbidden");
            return;
        }

        if (Directory.Exists(filePath))
        {
            filePath = Path.Combine(filePath, "index.html");
        }

        if (File.Exists(filePath))
        {
            string extension = Path.GetExtension(filePath).ToLower();
            if (ALLOWED_EXTENSIONS.Contains(extension))
            {
                var fileInfo = new FileInfo(filePath);
                string mimeType = MIME_TYPES.GetValueOrDefault(extension, "application/octet-stream");
                
                var headers = new Dictionary<string, string>
                {
                    ["Content-Length"] = fileInfo.Length.ToString(),
                    ["Last-Modified"] = fileInfo.LastWriteTimeUtc.ToString("R"),
                    ["Cache-Control"] = GetCacheControl(extension),
                    ["ETag"] = $"\"{fileInfo.Length}-{fileInfo.LastWriteTimeUtc.Ticks}\""
                };

                SendResponse(stream, 200, "OK", mimeType, Array.Empty<byte>(), headers);
            }
            else
            {
                SendErrorResponse(stream, 403, "Forbidden");
            }
        }
        else
        {
            SendErrorResponse(stream, 404, "Not Found");
        }
    }

    static void HandleOptionsRequest(NetworkStream stream)
    {
        var headers = new Dictionary<string, string>
        {
            ["Allow"] = "GET, HEAD, OPTIONS",
            ["Access-Control-Allow-Origin"] = "*",
            ["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS",
            ["Access-Control-Allow-Headers"] = "Content-Type"
        };

        SendResponse(stream, 200, "OK", "text/plain", Array.Empty<byte>(), headers);
    }

    static string? GetSafeFilePath(string requestPath)
    {
        try
        {
            // Remove query parameters and decode URL
            string cleanPath = requestPath.Split('?')[0];
            cleanPath = HttpUtility.UrlDecode(cleanPath);
            
            // Combine with web root
            string filePath = Path.Combine(WEB_ROOT, cleanPath.TrimStart('/'));
            filePath = Path.GetFullPath(filePath);
            // Ensure the file path is within the web root directory
            string webRootFullPath = Path.GetFullPath(WEB_ROOT);
            if (!filePath.StartsWith(webRootFullPath + Path.DirectorySeparatorChar) && 
                !filePath.Equals(webRootFullPath))
            {
                return null;
            }

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    static async Task<byte[]> GetFileContentAsync(string filePath)
    {
        string cacheKey = filePath + "|" + File.GetLastWriteTimeUtc(filePath).Ticks;
        
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out byte[]? cachedContent))
            {
                return cachedContent;
            }
        }

        byte[] content = await File.ReadAllBytesAsync(filePath);
        
        lock (_cacheLock)
        {            // Limit cache size to prevent memory issues
            if (_cache.Count > 100)
            {
                _cache.Clear();
            }
            _cache[cacheKey] = content;
        }

        return content;
    }

    static string GetCacheControl(string extension)
    {
        return extension switch
        {
            ".html" => "no-cache",
            ".css" or ".js" => "public, max-age=3600",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".ico" or ".svg" => "public, max-age=86400",
            _ => "public, max-age=300"
        };
    }

    static void SendResponse(NetworkStream stream, int statusCode, string statusText,
                           string contentType, byte[] content, Dictionary<string, string>? additionalHeaders = null)
    {
        var headers = new StringBuilder();
        headers.AppendLine($"HTTP/1.1 {statusCode} {statusText}");
        headers.AppendLine($"Content-Type: {contentType}");
        headers.AppendLine($"Content-Length: {content.Length}");
        headers.AppendLine($"Server: Enhanced-CSharp-WebServer/1.0");
        headers.AppendLine($"Date: {DateTime.UtcNow:R}");
        headers.AppendLine("Connection: close");

        if (additionalHeaders != null)
        {
            foreach (var header in additionalHeaders)
            {
                headers.AppendLine($"{header.Key}: {header.Value}");
            }
        }

        headers.AppendLine();

        byte[] headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (content.Length > 0)
        {
            stream.Write(content, 0, content.Length);
        }
        stream.Flush();
    }

    static void SendErrorResponse(NetworkStream stream, int statusCode, string statusText, 
                                Dictionary<string, string>? additionalHeaders = null)
    {
        string errorPage = $@"<!DOCTYPE html>
<html>
<head>
    <title>{statusCode} {statusText}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; background-color: #f5f5f5; }}
        .error-container {{ background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
        h1 {{ color: #d32f2f; margin-bottom: 20px; }}
        p {{ color: #666; line-height: 1.6; }}
        .error-code {{ font-size: 4em; font-weight: bold; color: #d32f2f; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class='error-container'>
        <div class='error-code'>{statusCode}</div>
        <h1>{statusText}</h1>
        <p>The requested resource could not be found or you don't have permission to access it.</p>
        <p><small>Enhanced C# Web Server</small></p>
    </div>
</body>
</html>";

        byte[] content = Encoding.UTF8.GetBytes(errorPage);
        SendResponse(stream, statusCode, statusText, "text/html", content, additionalHeaders);
    }

    static void LogRequest(HttpRequest request, string clientEndPoint)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] {clientEndPoint} - {request.Method} {request.RawUrl}");
    }
}

class HttpRequest
{
    public string Method { get; set; } = "";
    public string RawUrl { get; set; } = "";
    public string Path { get; set; } = "";
    public string Version { get; set; } = "";
    public Dictionary<string, string> Headers { get; set; } = new();
    public Dictionary<string, string> QueryString { get; set; } = new();
}