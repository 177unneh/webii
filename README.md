<img src="https://github.com/177unneh/webii/blob/master/webii/WebiColor.png?raw=true" height="150" width="150">
# WebII - Lightweight HTTP Server Library for .NET

WebII is a simple, lightweight HTTP server library for .NET applications that allows you to easily create web servers with static file serving, POST/PUT request handling, and file caching capabilities.

## Features

- 🌐 HTTP server with static file serving
- 📁 Automatic file caching with FileRam system
- 🔄 POST/PUT request handling
- 🌍 Multi-language file support
- ⚡ ETags and conditional requests support
- 🎯 Custom 404 page support
- 🗂️ Directory-based public file serving

## Quick Start

### 1. Basic Server Setup

```csharp
using System.Net;
using webii;

// Create a web server on localhost:8080
var server = new WebServer(IPAddress.Any, false, 8080, @"C:\MyWebsite");

// Start the server
server.Start();

Console.WriteLine("Server running on http://localhost:8080");
Console.ReadLine(); // this is when you have console app, without it app closes.
```

### 2. Static File Serving

WebII automatically serves static files from the `public` directory within your root directory:

```
MyWebsite/
├── public/
│   ├── index.html
│   ├── css/
│   │   └── style.css
│   ├── js/
│   │   └── app.js
│   └── images/
│       └── logo.png
```

Files are accessible at:
- `http://localhost:8080/` → serves `public/index.html`
- `http://localhost:8080/css/style.css` → serves `public/css/style.css`
- `http://localhost:8080/js/app.js` → serves `public/js/app.js`

### 3. Handling POST Requests

#### Method 1: Using Lambda Functions

```csharp
using webii.http;

var server = new WebServer(IPAddress.Any, false, 8080);

// Handle POST request to /api/users
server.Post("/api/users", (body, headers) =>
{
    Console.WriteLine($"Received POST data: {body}");
    
    // Parse JSON, process data, etc.
    var response = new Dictionary<string, string>
    {
        ["Content-Type"] = "application/json",
        ["Access-Control-Allow-Origin"] = "*"
    };
    
    return HttpResponse.CreateTextResponse("200 OK", response, 
        "{\"message\": \"User created successfully\", \"id\": 123}");
});

server.Start();
```

#### Method 2: Using Fluent Syntax

```csharp
// Using the fluent operator overload
server.Post("/api/login") += (body, headers) =>
{
    // Handle login logic
    var credentials = JsonSerializer.Deserialize<LoginRequest>(body);
    
    if (IsValidLogin(credentials))
    {
        var token = GenerateJwtToken(credentials.Username);
        return HttpResponse.CreateTextResponse("200 OK", 
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            $"{{\"token\": \"{token}\"}}");
    }
    
    return HttpResponse.CreateTextResponse("401 Unauthorized",
        new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        "{\"error\": \"Invalid credentials\"}");
};
```

#### Method 3: Using Handler Method

```csharp
server.Post("/api/upload")
    .Handler((body, headers) =>
    {
        // Handle file upload
        var contentType = headers.GetValueOrDefault("Content-Type", "");
        
        if (contentType.StartsWith("multipart/form-data"))
        {
            // Process file upload
            SaveUploadedFile(body);
            
            return HttpResponse.CreateTextResponse("200 OK",
                new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                "{\"message\": \"File uploaded successfully\"}");
        }
        
        return HttpResponse.CreateTextResponse("400 Bad Request",
            new Dictionary<string, string> { ["Content-Type"] = "application/json" },
            "{\"error\": \"Invalid content type\"}");
    });
```

### 4. Handling PUT Requests

```csharp
// Update user data
server.Put("/api/users/123", (body, headers) =>
{
    var userData = JsonSerializer.Deserialize<User>(body);
    
    // Update user in database
    UpdateUser(123, userData);
    
    return HttpResponse.CreateTextResponse("200 OK",
        new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        JsonSerializer.Serialize(userData));
});

// Using fluent syntax
server.Put("/api/settings") += (body, headers) =>
{
    var settings = JsonSerializer.Deserialize<AppSettings>(body);
    SaveSettings(settings);
    
    return HttpResponse.CreateTextResponse("204 No Content",
        new Dictionary<string, string>(), "");
};
```

### 5. Complete REST API Example

```csharp
using System.Net;
using System.Text.Json;
using webii;
using webii.http;

var server = new WebServer(IPAddress.Any, false, 3000, @"C:\MyApi");

// CORS headers for all responses
var corsHeaders = new Dictionary<string, string>
{
    ["Access-Control-Allow-Origin"] = "*",
    ["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS",
    ["Access-Control-Allow-Headers"] = "Content-Type, Authorization"
};

// GET all users (served as static JSON file from public/api/users.json)
// Just place a users.json file in public/api/ directory

// CREATE user
server.Post("/api/users") += (body, headers) =>
{
    try
    {
        var user = JsonSerializer.Deserialize<User>(body);
        user.Id = GenerateNewId();
        
        // Save to database/file
        SaveUser(user);
        
        var responseHeaders = new Dictionary<string, string>(corsHeaders)
        {
            ["Content-Type"] = "application/json"
        };
        
        return HttpResponse.CreateTextResponse("201 Created", responseHeaders,
            JsonSerializer.Serialize(user));
    }
    catch (Exception ex)
    {
        var errorHeaders = new Dictionary<string, string>(corsHeaders)
        {
            ["Content-Type"] = "application/json"
        };
        
        return HttpResponse.CreateTextResponse("400 Bad Request", errorHeaders,
            $"{{\"error\": \"{ex.Message}\"}}");
    }
};

// UPDATE user
server.Put("/api/users") += (body, headers) =>
{
    var user = JsonSerializer.Deserialize<User>(body);
    UpdateUser(user);
    
    var responseHeaders = new Dictionary<string, string>(corsHeaders)
    {
        ["Content-Type"] = "application/json"
    };
    
    return HttpResponse.CreateTextResponse("200 OK", responseHeaders,
        JsonSerializer.Serialize(user));
};

server.Start();
Console.WriteLine("API Server running on http://localhost:3000");
Console.ReadLine();

// Helper classes
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}
```

### 6. Custom 404 Page

```csharp
var server = new WebServer(IPAddress.Any, false, 8080, @"C:\MyWebsite");

// Set custom 404 page
server.Set404Page(@"C:\MyWebsite\public\404.html");

server.Start();
```

If nothing is selected, Webii will make page itself.


### 7. Multi-language Support

WebII automatically supports multi-language files based on Accept-Language headers:

```
public/
├── index.html          # Default
├── index.en.html       # English version
├── index.pl.html       # Polish version  
├── index.de.html       # German version
└── about/
    ├── index.html      # Default about page
    ├── index.en.html   # English about page
    └── index.pl.html   # Polish about page
```

The server automatically serves the appropriate language version based on the browser's `Accept-Language` header.
The language setting is before the extention, so `index.en.html` is served for English requests.
### 8. File Caching with FileRam

WebII includes an intelligent file caching system:

```csharp
var server = new WebServer(IPAddress.Any, false, 8080);

// FileRam automatically caches files and handles ETags
// Default cache size is 200MB, you can check cache status
Console.WriteLine($"Cache size: {server.FileRam}");

server.Start();
```

The FileRam system:
- Automatically caches frequently accessed files
- Handles ETags for conditional requests
- Monitors file changes with FileSystemWatcher
- Implements LRU cache eviction
- Supports both text and binary files

### 9. Advanced Configuration

```csharp
// Custom root directory and port
var server = new WebServer(
    ip: IPAddress.Parse("192.168.1.100"),  // Specific IP
    UseHttps: false,                        // HTTP only for now
    port: 9000,                            // Custom port
    rootDirectory: @"D:\MyWebApp"          // Custom root directory
);

// Multiple API endpoints
server.Post("/api/auth/login") += HandleLogin;
server.Post("/api/auth/register") += HandleRegister;
server.Put("/api/users/profile") += UpdateProfile;
server.Post("/api/files/upload") += HandleFileUpload;

server.Start();

// Handler methods
HttpResponse HandleLogin(string body, Dictionary<string, string> headers)
{
    // Login logic here
    return HttpResponse.CreateTextResponse("200 OK", 
        new Dictionary<string, string> { ["Content-Type"] = "application/json" },
        "{\"status\": \"success\"}");
}
```

### 10. Binary File Handling

WebII automatically handles binary files (images, PDFs, etc.):

```csharp
// Files in public directory are automatically served with correct MIME types:
// public/images/logo.png    → image/png
// public/docs/manual.pdf    → application/pdf  
// public/videos/demo.mp4    → video/mp4

// For API responses with binary data:
server.Post("/api/generate-pdf") += (body, headers) =>
{
    var pdfData = GeneratePdfReport(body);
    
    var responseHeaders = new Dictionary<string, string>
    {
        ["Content-Type"] = "application/pdf",
        ["Content-Disposition"] = "attachment; filename=report.pdf"
    };
    
    return HttpResponse.CreateBinaryResponse("200 OK", responseHeaders, pdfData);
};
```



## Configuration Options

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `ip` | `IPAddress` | Server IP address | Required |
| `UseHttps` | `bool` | Enable HTTPS (not implemented) | `false` |
| `port` | `int` | Server port | `80` |
| `rootDirectory` | `string` | Root directory path | `%AppData%\Webii` |

## MIME Types Supported

WebII automatically detects and serves these content types:

- `.html` → `text/html`
- `.css` → `text/css` 
- `.js` → `application/javascript`
- `.json` → `application/json`
- `.png` → `image/png`
- `.jpg/.jpeg` → `image/jpeg`
- `.gif` → `image/gif`
- `.ico` → `image/x-icon`
- Others → `application/octet-stream`

## Performance Features

- **File Caching**: Intelligent caching system with configurable memory limits
- **ETags**: Automatic ETag generation for conditional requests
- **File Watching**: Automatic cache invalidation on file changes
- **CORS Support**: Built-in CORS headers for API endpoints
- **Compression Ready**: Easy to extend with compression support

## Requirements

- .NET 9.0 or later
- Windows/Linux/macOS support

## License

This project is open source. Check the repository for license details.

## Contributing

Contributions are welcome! Please check the GitHub repository for contribution guidelines.

---

*WebII - Simple, fast, and lightweight HTTP server for .NET applications.*