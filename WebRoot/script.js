console.log("Enhanced C# Web Server - JavaScript loaded successfully!");

// Test functions for the demo page
function testAPI() {
    const output = document.getElementById('demo-output');
    output.innerHTML = '<p>Testing server capabilities...</p>';
    
    // Test different endpoints
    const tests = [
        { url: '/api/status.json', name: 'JSON API' },
        { url: '/test.txt', name: 'Text File' },
        { url: '/styles.css', name: 'CSS File' }
    ];
    
    tests.forEach(test => {
        fetch(test.url)
            .then(response => {
                const status = response.ok ? '✅' : '❌';
                const statusText = `${status} ${test.name}: ${response.status} ${response.statusText}`;
                output.innerHTML += `<p>${statusText}</p>`;
                return response.text();
            })
            .catch(error => {
                output.innerHTML += `<p>❌ ${test.name}: ${error.message}</p>`;
            });
    });
}

function loadDynamicContent() {
    const output = document.getElementById('demo-output');
    const timestamp = new Date().toLocaleString();
    
    output.innerHTML = `
        <div class="dynamic-content">
            <h3>Dynamic Content Loaded</h3>
            <p><strong>Timestamp:</strong> ${timestamp}</p>
            <p><strong>User Agent:</strong> ${navigator.userAgent}</p>
            <p><strong>Current URL:</strong> ${window.location.href}</p>
            <p><strong>Server:</strong> Enhanced C# Web Server</p>
        </div>
    `;
}

function showServerInfo() {
    const output = document.getElementById('demo-output');
    
    // Make a HEAD request to get server headers
    fetch('/', { method: 'HEAD' })
        .then(response => {
            let headersInfo = '<h3>Server Response Headers</h3><ul>';
            for (let [key, value] of response.headers.entries()) {
                headersInfo += `<li><strong>${key}:</strong> ${value}</li>`;
            }
            headersInfo += '</ul>';
            
            output.innerHTML = `
                <div class="server-info">
                    ${headersInfo}
                    <h3>Connection Info</h3>
                    <ul>
                        <li><strong>Status:</strong> ${response.status} ${response.statusText}</li>
                        <li><strong>Type:</strong> ${response.type}</li>
                        <li><strong>URL:</strong> ${response.url}</li>
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
    console.log('Page loaded, server is working correctly!');
    
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
});