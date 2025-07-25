using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using static webii.WebServer;

namespace webii.http
{
    internal class httphandler : IDisposable, Iwwwhandler
    {
        WebServer server;
        TcpListener listener;
        Thread ListeningThread;
        string page404;
        private CancellationTokenSource cancellationTokenSource;
        private bool isRunning = false;
        public httphandler(WebServer server)
        {
            this.server = server;
            cancellationTokenSource = new CancellationTokenSource();
            listener = new TcpListener(server.Host, server.Port);
            ListeningThread = new Thread(() =>
            {
                try
                {
                    Listening();
                }
                catch (Exception e)
                {
                    if (!cancellationTokenSource.IsCancellationRequested)
                    {
                        Console.WriteLine($"[ERR] An error occurred while listening for HTTP requests: {e.Message}");
                        Stop();
                        throw new Exception("An error occurred while listening for HTTP requests so its Stopped!" + e);
                    }
                }
            });
        }

        public void Dispose()
        {
            //throw new NotImplementedException();
            listener.Stop();
            listener.Dispose();
            listener = null;
        }

        public void Start()
        {
            if (!isRunning)
            {
                cancellationTokenSource = new CancellationTokenSource();
                listener.Start();
                isRunning = true;
                ListeningThread.Start();
            }
        }

        public void Stop()
        {
            if (isRunning)
            {
                isRunning = false;
                cancellationTokenSource.Cancel();
                listener.Stop();
                if (ListeningThread != null && ListeningThread.IsAlive)
                {
                    if (!ListeningThread.Join(3000))  // Czekamy max 3 sekundy
                    {
                        Console.WriteLine("[WARN] Listening thread did not terminate gracefully.");
                    }
                }
            }
        }

        async void Listening()
        {
            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        if (cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            client.Close();
                            break;
                        }

                        // Przekazujemy token anulowania do obsługi klienta
                        _ = HandleClient(client);
                        
                    }
                    catch (ObjectDisposedException)
                    {
                        // Normalne zachowanie, gdy listener jest zatrzymywany
                        break;
                    }
                    catch (SocketException ex) when (cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        // Ignorujemy błędy socketu podczas zamykania
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Console.WriteLine($"[ERR] Error accepting client: {ex.Message}");
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Console.WriteLine($"[ERR] Error in listening loop: {ex.Message}");
                }
            }
            finally
            {
                Console.WriteLine("[INFO] HTTP listener stopped");
            }
        }

        async Task HandleClient(TcpClient client)
        {

            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8,false,4096,true);

            string requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine))
                return;


            string[] REQParts = requestLine.Split(' ', 3);
            if (REQParts.Length < 3)
            {
                // Obsługa nieprawidłowego żądania
                Console.WriteLine("[ERR] Invalid request method or format.");
                return;
            }

            string headerLine;
            Dictionary<string, string> headers = new Dictionary<string, string>();
            while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
            {
                var headerParts = headerLine.Split(new[] { ':' }, 2);
                if (headerParts.Length == 2)
                {
                    headers[headerParts[0].Trim()] = headerParts[1].Trim();
                }
                headerParts = null;
            }
            //Console.WriteLine("[HEADERS] Request headers:");
            string requestBody = "";
            if ((REQParts[0] == "POST" || REQParts[0] == "PUT") &&
                headers.TryGetValue("Content-Length", out string contentLengthStr) &&
                int.TryParse(contentLengthStr, out int contentLength))
            {
                char[] bodyBuffer = new char[contentLength];
                await reader.ReadAsync(bodyBuffer, 0, contentLength);
                requestBody = new string(bodyBuffer);
                headers["RequestBody"] = requestBody; // Dodaj ciało do headers
            }
            string GetOrSmth = REQParts[1];
            //Console.WriteLine($"[REQ] Method: {REQParts[0]}, Path: {GetOrSmth}, Version: {REQParts[2]}");
            try
            {
                REQType requestType = Enum.TryParse(REQParts[0], out REQType type) ? type : REQType.GET;

                var response = RequestType(requestType, REQParts[1], headers);

                using (response)
                {
                    if (response.IsBinary)
                    {
                        await stream.WriteAsync(response.HeaderBytes);
                        if (response.Body != null && response.Body.Length > 0)
                        {
                            await stream.WriteAsync(response.Body);
                        }
                    }
                    else
                    {
                        byte[] responseBytes = Encoding.UTF8.GetBytes(response.TextResponse);
                        await stream.WriteAsync(responseBytes);
                    }
                }

            }
            catch (Exception ea)
            {
                Console.WriteLine("ERR" + ea);
                //throw;
                HttpResponse response = HttpResponse.CreateTextResponse("500 Internal Server Error", new Dictionary<string, string>
                {
                    ["Content-Type"] = "text/html; charset=UTF-8",
                    ["Connection"] = "close",
                    ["Server"] = "Webii/" + WebServer.VersionNumber
                }, "<h1>500 Internal Server Error</h1> <p>Webii</p>");
                var responseBytes = Encoding.UTF8.GetBytes(response.TextResponse);
                await stream.WriteAsync(responseBytes);
                response.Dispose();
                response = null;
                ea = null;
            }
            client.Close();
            reader.Dispose();
            stream.Dispose();
            client.Dispose();
            GetOrSmth = null;
            requestLine = null;
            headers.Clear();
            requestBody = null;
        }
        HttpResponse RequestType(REQType type, string path, Dictionary<string, string> headers)
        {
            string LanguageSelected = headers.TryGetValue("Accept-Language", out string acceptLanguage)
             ? acceptLanguage
             : "";
            switch (type)
            {

                case REQType.GET:

                    if (server.GetHandlers.TryGetValue(path, out var handler))
                    {
                        try
                        {
                            // Wywołaj własny handler
                            return handler(headers.TryGetValue("RequestBody", out var body) ? body : "", headers);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERR] Error in GET handler for {path}: {ex.Message}");
                            return HttpResponse.CreateTextResponse("500 Internal Server Error", new Dictionary<string, string>
                            {
                                ["Content-Type"] = "application/json",
                                ["Access-Control-Allow-Origin"] = "*",
                                ["Connection"] = "close",
                                ["Server"] = "Webii/" + WebServer.VersionNumber
                            }, "{\"error\":\"Internal server error\"}");
                        }
                    }
                    string fullPath = "";
                    if (path.EndsWith("/"))
                    {
                        fullPath = Path.Combine(server.publicDirectory, path.TrimStart('/'), "index.html");
                    }
                    else
                    {
                        fullPath = Path.Combine(server.publicDirectory, path.TrimStart('/'));
                    }
                    string Extension = Path.GetExtension(fullPath).ToLower();
                    if (string.IsNullOrEmpty(Extension))
                    {
                        // Jeśli nie ma rozszerzenia, dodaj domyślne rozszerzenie
                        fullPath += ".html";
                    }
                    if(string.IsNullOrEmpty(Extension))
                    {
                        Extension = ".html";
                    }

                    string aaa = "\\";
                    fullPath = fullPath.Replace(Extension, "").Replace("/", "/").Replace(aaa, "/");
                    List<string> languages = LanguageSelected
                    .Split(',')
                    .Select(part =>
                    {
                        var lang = part.Split(';')[0].Trim();
                        var q = part.Contains(";q=")
                            ? double.Parse(part.Split(";q=")[1], System.Globalization.CultureInfo.InvariantCulture)
                            : 1.0;
                        return (lang, q);
                    })
                    .OrderByDescending(x => x.q)
                    .Select(x => x.lang)
                    .ToList();

                    string folder = Path.GetDirectoryName(fullPath);

                    bool Translate = true;
                    if (File.Exists(Path.Combine(folder, ".NoTranslate")))
                    {
                        Translate = false;
                    }

                    if (Translate == true)
                    {
                        foreach (var lang in languages)
                        {
                            string preferencelangugage = fullPath + "." + lang + Extension;

                            HttpResponse LoadedPagePRobally = ReturnPageByPath(preferencelangugage, headers);
                            if (LoadedPagePRobally != null)
                            {
                                return LoadedPagePRobally;
                            }
                        }
                    }

                    // Try without translation
                    fullPath = fullPath + Extension;

                    if (!File.Exists(fullPath))
                    {
                        return HttpResponse.CreateTextResponse("404 Not Found", new Dictionary<string, string>
                        {
                            ["Content-Type"] = "text/html; charset=UTF-8",
                            ["Connection"] = "close",
                            ["Server"] = "Webii/1.0"
                        }, Return404());
                    }
                    HttpResponse pa = ReturnPageByPath(fullPath, headers);
                    if (pa != null)
                    {
                        //Console.WriteLine($"[REQ] Page found for language: {fullPath}");
                        return pa;
                    }
                    else
                    {
                    }

                    return HttpResponse.CreateTextResponse("404 Not Found", new Dictionary<string, string>
                    {
                        ["Content-Type"] = "text/html; charset=UTF-8",
                        ["Connection"] = "close",
                        ["Server"] = "Webii/1.0"
                    }, Return404());

                case REQType.POST:
                    return HandlePostRequest(path, headers);
                case REQType.PUT:
                    return HandlePutRequest(path, headers);
                case REQType.OPTIONS:
                    return HandleOptionsRequest(path, headers);

                default:
                    break;
            }
            return HttpResponse.CreateTextResponse("500 Internal Server Error", new Dictionary<string, string>
            {
                ["Content-Type"] = "text/html; charset=UTF-8",
                ["Connection"] = "close",
                ["Server"] = "Webii/1.0"
            }, "<h1>500 Internal Server Error</h1> <p>Webii</p>");

        }
        private HttpResponse HandleOptionsRequest(string path, Dictionary<string, string> headers)
        {
            var corsHeaders = new Dictionary<string, string>
            {
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS",
                ["Access-Control-Allow-Headers"] = "Content-Type, Authorization",
                ["Access-Control-Max-Age"] = "86400",
                ["Connection"] = "close",
                ["Content-Length"] = "0",
                ["Server"] = "Webii/" + WebServer.VersionNumber
            };

            return HttpResponse.CreateTextResponse("200 OK", corsHeaders, "");
        }
        HttpResponse ReturnPageByPath(string path, Dictionary<string, string> requestHeaders = null)
        {
            if (File.Exists(path))
            {
                string extension = Path.GetExtension(path).ToLower();
                string contentType = extension switch
                {
                    ".html" => "text/html",
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".ico" => "image/x-icon",
                    ".pdf" => "application/pdf",
                    ".json" => "application/json",
                    ".js" => "application/javascript",
                    ".mp4" => "video/mp4",
                    ".webm" => "video/webm",
                    ".ogg" => "video/ogg",
                    ".avi" => "video/x-msvideo",
                    ".mpeg" => "video/mpeg",
                    ".mpg" => "video/mpeg",
                    ".mov" => "video/quicktime",
                    ".flv" => "video/x-flv",
                    ".wmv" => "video/x-ms-wmv",
                    ".mkv" => "video/x-matroska",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    ".csv" => "text/csv",
                    ".mp3" => "audio/mpeg",

                    ".zip" => "application/zip",
                    ".txt" => "text/plain",
                    ".xml" => "application/xml",
                    ".rar" => "application/x-rar-compressed",
                    ".doc" => "application/msword",
                    ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ".xls" => "application/vnd.ms-excel",
                    ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    ".ppt" => "application/vnd.ms-powerpoint",
                    ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                    _ => "application/octet-stream" // Domyślny typ dla nieznanych plików
                };
                if (contentType.StartsWith("image/") || contentType.StartsWith("video/") || contentType.StartsWith("audio/") || contentType.StartsWith("application"))
                {
                    try
                    {
                        // Mechanizm retry dla plików binarnych (obrazy, wideo, PDF)
                        byte[] fileData = null;
                        int retryCount = 3;

                        while (retryCount > 0 && fileData == null)
                        {
                            fileData = server.FileRam.GetBytes(path);
                            if (fileData == null)
                            {
                                Console.WriteLine($"[WARN] Failed to load binary data, retrying... ({retryCount} attempts left)");
                                Thread.Sleep(10); // Krótka pauza przed ponowną próbą
                                retryCount--;
                            }
                        }

                        if (fileData == null)
                        {
                            Console.WriteLine($"[ERR] Failed to load file after retries: {path}");
                            return null;
                        }

                        // Sprawdź czy dane są prawidłowe
                        if (fileData.Length == 0)
                        {
                            Console.WriteLine($"[ERR] Empty file: {path}");
                            return null;
                        }

                        var lastModified = server.FileRam.GetLastModified(path);
                        var etag = server.FileRam.GetETag(path);

                        // Sprawdź cache klienta
                        if (requestHeaders != null && IsNotModified(requestHeaders, lastModified, etag))
                        {
                            var notModifiedHeaders = new Dictionary<string, string>
                            {
                                ["Connection"] = "close",
                                ["Server"] = "Webii/" + WebServer.VersionNumber
                            };

                            if (etag != null)
                                notModifiedHeaders["ETag"] = etag;

                            if (lastModified.HasValue)
                                notModifiedHeaders["Last-Modified"] = lastModified.Value.ToString("R");

                            return HttpResponse.CreateTextResponse("304 Not Modified", notModifiedHeaders, "");
                        }

                        var responseHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = contentType,
                            ["Content-Length"] = fileData.Length.ToString(),
                            ["Connection"] = "close",
                            ["Server"] = "Webii/" + WebServer.VersionNumber,
                            ["Cache-Control"] = "public, max-age=3600", // Cache na 1 godzinę
                            ["Accept-Ranges"] = "bytes"
                        };

                        if (contentType.StartsWith("audio/"))
                        {
                            responseHeaders["Content-Disposition"] = "inline";
                        }

                        if (etag != null)
                            responseHeaders["ETag"] = etag;

                        if (lastModified.HasValue)
                            responseHeaders["Last-Modified"] = lastModified.Value.ToString("R");

                        string fileType = contentType.StartsWith("image/") ? "image" :
                  (contentType.StartsWith("video/") ? "video" :
                  (contentType.StartsWith("audio/") ? "audio" : "document"));


                        return HttpResponse.CreateBinaryResponse("200 OK", responseHeaders, fileData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERR] Error serving file {path}: {ex.Message}");
                        return null;
                    }
                }

                else if (contentType == "text/html")
                {
                    // ZMIANA: Pobierz zawartość PRZED sprawdzeniem cache'a
                    string htmlContent = server.FileRam.GetText(path);
                    if (htmlContent != null)
                    {
                        // Get file metadata for caching
                        var lastModified = server.FileRam.GetLastModified(path);
                        var etag = server.FileRam.GetETag(path);

                        // Check if client has cached version
                        if (requestHeaders != null && IsNotModified(requestHeaders, lastModified, etag))
                        {
                            var notModifiedHeaders = new Dictionary<string, string>
                            {
                                ["Connection"] = "close",
                                ["Server"] = "Webii/" + WebServer.VersionNumber
                            };

                            if (etag != null)
                                notModifiedHeaders["ETag"] = etag;

                            if (lastModified.HasValue)
                                notModifiedHeaders["Last-Modified"] = lastModified.Value.ToString("R");

                            return HttpResponse.CreateTextResponse("304 Not Modified", notModifiedHeaders, "");
                        }

                        var responseHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = contentType,
                            ["Content-Length"] = Encoding.UTF8.GetByteCount(htmlContent).ToString(),
                            ["Connection"] = "close",
                            ["Server"] = "Webii/" + WebServer.VersionNumber
                        };

                        if (etag != null)
                            responseHeaders["ETag"] = etag;

                        if (lastModified.HasValue)
                            responseHeaders["Last-Modified"] = lastModified.Value.ToString("R");

                        return HttpResponse.CreateTextResponse("200 OK", responseHeaders, htmlContent);
                    }
                }
                else
                {
                    Console.WriteLine("#####################" + contentType + "not suppored");
                    return HttpResponse.CreateTextResponse("415 Unsupported Media Type", new Dictionary<string, string>
                    {
                        ["Content-Type"] = "text/html; charset=UTF-8",
                        ["Connection"] = "close",
                        ["Server"] = "Webii/"+ WebServer.VersionNumber
                    }, "<h1>415 Unsupported Media Type</h1> <p>Webii</p>");
                }
            }
            else
            {
            }
            return null;
        }

        private bool IsNotModified(Dictionary<string, string> requestHeaders, DateTime? lastModified, string etag)
        {

            if (lastModified.HasValue && requestHeaders.TryGetValue("If-Modified-Since", out string ifModifiedSince))
            {
                if (DateTime.TryParse(ifModifiedSince, out DateTime clientDate))
                {
                    // Remove milliseconds for comparison (HTTP dates don't include them)
                    var serverDate = new DateTime(lastModified.Value.Ticks - (lastModified.Value.Ticks % TimeSpan.TicksPerSecond));
                    bool isNotModified = serverDate <= clientDate;
                    if (isNotModified)
                    {
                        return true;
                    }
                    else
                    {
                    }
                }
                else
                {
                }
            }
            else
            {
            }

            // Check If-None-Match header (ETag)
            if (etag != null && requestHeaders.TryGetValue("If-None-Match", out string ifNoneMatch))
            {
                bool etagMatch = ifNoneMatch == etag || ifNoneMatch == "*";
                if (etagMatch)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            // Check If-Modified-Since header
           

            return false;
        }


        private HttpResponse HandlePostRequest(string path, Dictionary<string, string> headers)
        {
            string requestBody = headers.ContainsKey("RequestBody") ? headers["RequestBody"] : "";

            // Dodaj nagłówki CORS do każdej odpowiedzi POST
            var corsHeaders = new Dictionary<string, string>
            {
                ["Access-Control-Allow-Origin"] = "*",
                ["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS",
                ["Access-Control-Allow-Headers"] = "Content-Type, Authorization",
                ["Content-Type"] = "application/json",
                ["Connection"] = "close",
                ["Server"] = "Webii/" + WebServer.VersionNumber
            };

            if (server.PostHandlers.ContainsKey(path))
            {
                try
                {
                    var response = server.PostHandlers[path](requestBody, headers);

                    // Dodaj nagłówki CORS do istniejącej odpowiedzi
                    if (response.IsBinary)
                    {
                        // Dla odpowiedzi binarnych, dodaj nagłówki CORS do HeaderBytes
                        var headerString = Encoding.UTF8.GetString(response.HeaderBytes);
                        foreach (var header in corsHeaders)
                        {
                            if (!headerString.Contains(header.Key))
                            {
                                headerString = headerString.Replace("\r\n\r\n", $"\r\n{header.Key}: {header.Value}\r\n\r\n");
                            }
                        }
                        response.HeaderBytes = Encoding.UTF8.GetBytes(headerString);
                    }
                    else
                    {
                        // Dla odpowiedzi tekstowych, dodaj nagłówki CORS
                        var lines = response.TextResponse.Split('\n');
                        var headerEndIndex = Array.FindIndex(lines, line => string.IsNullOrWhiteSpace(line));

                        if (headerEndIndex > 0)
                        {
                            var headerLines = new List<string>(lines.Take(headerEndIndex));
                            foreach (var header in corsHeaders)
                            {
                                if (!headerLines.Any(line => line.StartsWith(header.Key + ":")))
                                {
                                    headerLines.Add($"{header.Key}: {header.Value}");
                                }
                            }
                            headerLines.Add(""); // Pusta linia oddzielająca nagłówki od body
                            headerLines.AddRange(lines.Skip(headerEndIndex + 1));
                            response.TextResponse = string.Join("\n", headerLines);
                        }
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERR] Error in POST handler for {path}: {ex.Message}");
                    return HttpResponse.CreateTextResponse("500 Internal Server Error", corsHeaders,
                        "{\"error\":\"Internal server error\"}");
                }
            }

            return HttpResponse.CreateTextResponse("404 Not Found", corsHeaders,
                "{\"error\":\"Endpoint not found\"}");
        }

        private HttpResponse HandlePutRequest(string path, Dictionary<string, string> headers)
        {
            string requestBody = headers.ContainsKey("RequestBody") ? headers["RequestBody"] : "";

            if (server.PutHandlers.ContainsKey(path))
            {
                return server.PutHandlers[path](requestBody, headers);
            }

            return HttpResponse.CreateTextResponse("404 Not Found", new Dictionary<string, string>
            {
                ["Content-Type"] = "text/html; charset=UTF-8",
                ["Connection"] = "close",
                ["Server"] = "Webii/" + WebServer.VersionNumber
            }, Return404());
        }

        private string ReadRequestBody(Dictionary<string, string> headers)
        {
            // Implementacja odczytu ciała żądania na podstawie Content-Length
            if (headers.TryGetValue("Content-Length", out string contentLengthStr) &&
                int.TryParse(contentLengthStr, out int contentLength))
            {
                // Tu musisz dodać logikę odczytu ciała żądania z stream
                // To wymaga rozszerzenia metody HandleClient
                return ""; // Placeholder
            }
            return "";
        }


        public void Set404Page(string path)
        {
            page404 = path;
        }

        string Return404()
        {
            if (string.IsNullOrEmpty(page404))
            {
                return "<!DOCTYPE html>\r\n<html lang=\"en\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>404 Not found</title>\r\n    <style>\r\n        body {\r\n            background-color: #1f1f1f;\r\n        }\r\n       h1 {\r\n            text-align: center;\r\n            margin-top: 5%;\r\n            font-size: 5em;\r\n            font-family: Arial, sans-serif;\r\n            animation: rainbowText 13s ease-in-out infinite;\r\n        }\r\n          img {\r\n            display: block;\r\n            margin: 2% auto;\r\n            max-width: 300px;\r\n            height: auto;\r\n        }\r\n        @keyframes rainbowText {\r\n            0% { color: rgb(255, 100, 100); }\r\n            14.3% { color: rgb(255, 165, 100); }\r\n            28.6% { color: rgb(255, 255, 100); }\r\n            42.9% { color: rgb(100, 255, 100); }\r\n            57.2% { color: rgb(100, 255, 255); }\r\n            71.5% { color: rgb(100, 100, 255); }\r\n            85.8% { color: rgb(255, 100, 255); }\r\n            100% { color: rgb(255, 100, 100); }\r\n        }\r\n        h2 {\r\n            color: #c2b1ff;\r\n            text-align: center;\r\n            font-size: 2em;\r\n            font-family: Arial, sans-serif;\r\n        }\r\n        h4 {\r\n            color: #8d6dff;\r\n            text-align: center;\r\n            font-family: Arial, sans-serif;\r\n            position: fixed;\r\n            bottom: 20px;\r\n            left: 50%;\r\n            transform: translateX(-50%);\r\n            margin: 0;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1>Not found 404 :(</h1>\r\n    <h2>We're sorry, but the page you were looking for doesn't exist.</h2>\r\n    <img src=\"data:image/png;base64,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\" alt=\"webii\">\r\n    <h4>This is auto-generated page for 404 by Webii <a href=\"https://github.com/177unneh/webii\">github</a><</h4>\r\n \r\n</body>\r\n</html>";
                //return "<!DOCTYPE html>\r\n<html lang=\"en\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>404 Not found</title>\r\n    <style>\r\n        body {\r\n            background-color: #1f1f1f;\r\n        }\r\n       h1 {\r\n            text-align: center;\r\n            margin-top: 5%;\r\n            font-size: 5em;\r\n            font-family: Arial, sans-serif;\r\n            animation: rainbowText 13s ease-in-out infinite;\r\n        }\r\n        @keyframes rainbowText {\r\n            0% { color: rgb(255, 100, 100); }\r\n            14.3% { color: rgb(255, 165, 100); }\r\n            28.6% { color: rgb(255, 255, 100); }\r\n            42.9% { color: rgb(100, 255, 100); }\r\n            57.2% { color: rgb(100, 255, 255); }\r\n            71.5% { color: rgb(100, 100, 255); }\r\n            85.8% { color: rgb(255, 100, 255); }\r\n            100% { color: rgb(255, 100, 100); }\r\n        }\r\n        h2 {\r\n            color: #c2b1ff;\r\n            text-align: center;\r\n            font-size: 2em;\r\n            font-family: Arial, sans-serif;\r\n        }\r\n        h4 {\r\n            color: #8d6dff;\r\n            text-align: center;\r\n            font-family: Arial, sans-serif;\r\n            position: fixed;\r\n            bottom: 20px;\r\n            left: 50%;\r\n            transform: translateX(-50%);\r\n            margin: 0;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1>Not found 404 :(</h1>\r\n    <h2>We're sorry, but the page you were looking for doesn't exist.</h2>\r\n\r\n    <h4>This is auto-generated page for 404 by Webii <a href=\"https://github.com/177unneh/webii\">github</a><</h4>\r\n \r\n</body>\r\n</html>";
                //return "<!DOCTYPE html>\r\n<html lang=\"en\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>404 Not found</title>\r\n    <style>\r\n        body {\r\n            background-color: #1f1f1f;\r\n        }\r\n        h1 {\r\n            color: #ba85ff;\r\n            text-align: center;\r\n            margin-top: 5%;\r\n            font-size: 5em;\r\n            font-family: Arial, sans-serif;\r\n        }\r\n        h2 {\r\n            color: #c2b1ff;\r\n            text-align: center;\r\n            font-size: 2em;\r\n            font-family: Arial, sans-serif;\r\n        }\r\n        h4 {\r\n            color: #8d6dff;\r\n            text-align: center;\r\n            font-family: Arial, sans-serif;\r\n            position: fixed;\r\n            bottom: 20px;\r\n            left: 50%;\r\n            transform: translateX(-50%);\r\n            margin: 0;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n    <h1>Not found 404 :(</h1>\r\n    <h2>We're sorry, but the page you were looking for doesn't exist.</h2>\r\n\r\n    <h4>This is auto-generated page for 404 by Webii <a href=\"https://github.com/177unneh/webii\">github</a><</h4>\r\n \r\n</body>\r\n</html>";
            }
            else
            {
                try
                {
                    return server.FileRam.GetText(page404);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[ERR] Error reading 404 page: {e.Message}");
                    return "<h1>404 Not Found</h1><p>The requested resource was not found on this server.</p>";
                }
            }
        }
    }

    
}
