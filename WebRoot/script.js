console.log("Enhanced C# Web Server - JavaScript loaded successfully!");

// Test functions for the demo page
function testAPI() {
    const output = document.getElementById('demo-output');
    output.innerHTML = '<p>🔍 Testing server capabilities...</p>';
    
    // Test different endpoints that actually exist
    const tests = [
        { url: '/api/status.json', name: 'JSON API' },
        { url: '/test.txt', name: 'Text File' },
        { url: '/styles.css', name: 'CSS File' },
        { url: '/test.html', name: 'HTML File' }
    ];
    
    let completedTests = 0;
    
    tests.forEach(test => {
        fetch(test.url)
            .then(response => {
                const status = response.ok ? '✅' : '❌';
                const statusText = `${status} ${test.name}: ${response.status} ${response.statusText}`;
                output.innerHTML += `<p>${statusText}</p>`;
                
                completedTests++;
                if (completedTests === tests.length) {
                    output.innerHTML += '<p><strong>🎉 All tests completed!</strong></p>';
                }
                
                return response.text();
            })
            .catch(error => {
                const statusText = `❌ ${test.name}: ${error.message}`;
                output.innerHTML += `<p>${statusText}</p>`;
                
                completedTests++;
                if (completedTests === tests.length) {
                    output.innerHTML += '<p><strong>⚠️ Tests completed with errors</strong></p>';
                }
            });
    });
}

function loadDynamicContent() {
    const output = document.getElementById('demo-output');
    const timestamp = new Date().toLocaleString();
    
    output.innerHTML = `
        <div class="dynamic-content">
            <h3>✨ Dynamic Content Loaded</h3>
            <p><strong>Timestamp:</strong> ${timestamp}</p>
            <p><strong>User Agent:</strong> ${navigator.userAgent.substring(0, 100)}...</p>
            <p><strong>Current URL:</strong> ${window.location.href}</p>
            <p><strong>Screen Resolution:</strong> ${screen.width}x${screen.height}</p>
            <p><strong>Viewport Size:</strong> ${window.innerWidth}x${window.innerHeight}</p>
            <p><strong>Server:</strong> Enhanced C# Web Server</p>
            <p><strong>Connection:</strong> ${navigator.onLine ? 'Online' : 'Offline'}</p>
        </div>
    `;
}

function showServerInfo() {
    const output = document.getElementById('demo-output');
    output.innerHTML = '<p>🔍 Fetching server information...</p>';
    
    // Make a HEAD request to get server headers
    fetch('/', { method: 'HEAD' })
        .then(response => {
            let headersInfo = '<h3>📋 Server Response Headers</h3><ul>';
            for (let [key, value] of response.headers.entries()) {
                headersInfo += `<li><strong>${key}:</strong> ${value}</li>`;
            }
            headersInfo += '</ul>';
            
            output.innerHTML = `
                <div class="server-info">
                    ${headersInfo}
                    <h3>🔗 Connection Info</h3>
                    <ul>
                        <li><strong>Status:</strong> ${response.status} ${response.statusText}</li>
                        <li><strong>Type:</strong> ${response.type}</li>
                        <li><strong>URL:</strong> ${response.url}</li>
                        <li><strong>Redirected:</strong> ${response.redirected ? 'Yes' : 'No'}</li>
                    </ul>
                    <h3>🕐 Performance</h3>
                    <ul>
                        <li><strong>Navigation Type:</strong> ${performance.navigation.type}</li>
                        <li><strong>Page Load Time:</strong> ${Math.round(performance.now())}ms</li>
                    </ul>
                </div>
            `;
        })
        .catch(error => {
            output.innerHTML = `<p>❌ Error getting server info: ${error.message}</p>`;
        });
}

// Auto-load demo when page loads
document.addEventListener('DOMContentLoaded', function() {
    console.log('✅ Page loaded, server is working correctly!');
    
    // Add some interactive features
    const buttons = document.querySelectorAll('button');
    buttons.forEach(button => {
        button.addEventListener('click', function() {
            this.style.transform = 'scale(0.95)';
            setTimeout(() => {
                this.style.transform = 'scale(1)';
            }, 150);
        });
    });
    
    // Show initial server status
    setTimeout(() => {
        const output = document.getElementById('demo-output');
        if (output.innerHTML.trim() === '') {
            output.innerHTML = '<p>🚀 Server is ready! Click buttons above to test functionality.</p>';
        }
    }, 1000);
});