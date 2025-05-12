using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    const int PORT = 8080;
    const string WEB_ROOT = "WebRoot";
    static readonly string[] ALLOWED_EXTENSIONS = { ".html", ".css", ".js" };
    static readonly Dictionary<string, string> MIME_TYPES = new()
    {
        [".html"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript"
    };

    static void Main(string[] args)
    {
        TcpListener listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        Console.WriteLine($"Server running on port {PORT}...");

        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            ThreadPool.QueueUserWorkItem(HandleClient, client);
        }
    }

    static void HandleClient(object? state)
    {
        using TcpClient client = (TcpClient)state!;
        using NetworkStream stream = client.GetStream();

        try
        {
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            // Parse request line
            string[] requestLines = request.Split('\n');
            string[] requestParts = requestLines[0].Split(' ');
            if (requestParts.Length < 3) return;

            string method = requestParts[0];
            string path = requestParts[1];

            // Validate method
            if (method != "GET")
            {
                SendErrorResponse(stream, 405, "Method Not Allowed");
                return;
            }

            // Sanitize and validate path
            string sanitizedPath = Path.Combine(WEB_ROOT, path.TrimStart('/'));
            sanitizedPath = Path.GetFullPath(sanitizedPath);

            if (!sanitizedPath.StartsWith(Path.GetFullPath(WEB_ROOT)))
            {
                SendErrorResponse(stream, 403, "Forbidden");
                return;
            }

            // Handle default document
            if (Directory.Exists(sanitizedPath))
                sanitizedPath = Path.Combine(sanitizedPath, "index.html");

            // Validate file extension
            string extension = Path.GetExtension(sanitizedPath).ToLower();
            if (!ALLOWED_EXTENSIONS.Contains(extension))
            {
                SendErrorResponse(stream, 403, "Forbidden");
                return;
            }

            // Serve file
            if (File.Exists(sanitizedPath))
            {
                byte[] content = File.ReadAllBytes(sanitizedPath);
                string mimeType = MIME_TYPES.GetValueOrDefault(extension, "text/plain");
                SendResponse(stream, 200, "OK", mimeType, content);
            }
            else
            {
                SendErrorResponse(stream, 404, "Not Found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    static void SendResponse(NetworkStream stream, int statusCode, string statusText,
                           string contentType, byte[] content)
    {
        string headers = $"HTTP/1.1 {statusCode} {statusText}\r\n" +
                        $"Content-Type: {contentType}\r\n" +
                        $"Content-Length: {content.Length}\r\n" +
                        "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(content, 0, content.Length);
    }

    static void SendErrorResponse(NetworkStream stream, int statusCode, string statusText)
    {
        string errorPage = $@"
            <html>
                <head><title>{statusCode} {statusText}</title></head>
                <body>
                    <h1>Error {statusCode}: {statusText}</h1>
                </body>
            </html>";

        byte[] content = Encoding.UTF8.GetBytes(errorPage);
        SendResponse(stream, statusCode, statusText, "text/html", content);
    }
}