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
        public httphandler(WebServer server)
        {
            this.server = server;
            listener = new TcpListener(server.Host, server.Port);
            ListeningThread = new Thread(() =>
            {
                try
                {
                    Listening();
                }
                catch (Exception e)
                {
                    Stop();
                    Console.WriteLine($"[ERR] An error occurred while listening for HTTP requests: {e.Message}");
                    throw new Exception("An error occurred while listening for HTTP requests so its Stoped!" + e);
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
            listener.Start();
            ListeningThread.Start();

        }

        public void Stop()
        {
            listener.Stop();
            ListeningThread.Suspend();
        }

        async void Listening()
        {
            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClient(client);

            }
        }

        async Task HandleClient(TcpClient client)
        {
            using NetworkStream stream = client.GetStream();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            using StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            string requestLine = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(requestLine))
                return;

            Console.WriteLine($"[REQ] {requestLine}");

            string[] REQParts = requestLine.Split(' ',3);
            if (REQParts.Length < 3 || REQParts[0] != "GET")
            {
                // Obsługa nieprawidłowego żądania
                Console.WriteLine("[ERR] Invalid request method or format.");
                return;
            }

            string headerLine;
            Dictionary<string, string> headers = new Dictionary<string, string>();
            while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync()))
            {
                //Console.WriteLine($"[HEADER] {headerLine}");
                var headerParts = headerLine.Split(new[] { ':' }, 2);
                if (headerParts.Length == 2)
                {
                    headers[headerParts[0].Trim()] = headerParts[1].Trim();
                }
            }

            // Przykład: Wypisanie User-Agent
            if (headers.TryGetValue("User-Agent", out string userAgent))
            {
                //Console.WriteLine($"[INFO] User-Agent: {userAgent}");
            }

            string GetOrSmth = REQParts[1];
            Console.WriteLine($"[REQ] Method: {REQParts[0]}, Path: {GetOrSmth}, Version: {REQParts[2]} aaa ");
            string Rensponce = RequestType(Enum.TryParse(REQParts[0], out REQType type) ? type : REQType.GET, REQParts[1], headers);

            await writer.WriteAsync(Rensponce);
            client.Close();
        }
        string RequestType(REQType type, string path, Dictionary<string, string> headers)
        {
            string LanguageSelected = headers.TryGetValue("Accept-Language", out string acceptLanguage)
     ? acceptLanguage
     : "Not specified";
            switch (type)
            {

                case REQType.GET:
                    Console.WriteLine("[REQ] GET request received. "+ path);

                    string fullPath = "";

                    if (path.EndsWith("/"))
                    {
                        fullPath = Path.Combine(server.publicDirectory, path.TrimStart('/'), "index.html");
                    }
                    else
                    {
                        fullPath = Path.Combine(server.publicDirectory, path.TrimStart('/'));
                    }
                    Console.WriteLine($"[REQ] Full path resolved to: "+ LanguageSelected);

                    string Extension = Path.GetExtension(fullPath).ToLower();   
                    if (string.IsNullOrEmpty(Extension))
                    {
                        // Jeśli nie ma rozszerzenia, dodaj domyślne rozszerzenie
                        fullPath += ".html";
                    }
                    fullPath = fullPath.Replace(Extension, "").Replace("/", "");

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
                    if (File.Exists(Path.Combine(folder,".NoTranslate")))
                    {
                        Translate = false;
                    }

                    if(Translate == true)
                    {
                        foreach (var lang in languages)
                        {
                            string preferencelangugage = fullPath + "." + lang + Extension;

                            string LoadedPagePRobally = ReturnPageByPath(preferencelangugage);
                            if (LoadedPagePRobally != null)
                            {
                                Console.WriteLine($"[REQ] Page found for language: {LoadedPagePRobally}");

                                return LoadedPagePRobally;
                            }
                            else
                                Console.WriteLine($"[REQ] Page not found for language: {LoadedPagePRobally}");
                            {
                            }
                        }
                    }
                   
                    // Try without translation
                    fullPath = fullPath + Extension;
                    string pa = ReturnPageByPath(fullPath);
                    if (pa != null)
                    {
                        Console.WriteLine($"[REQ] Page found for language: {fullPath}");
                        return pa;
                    }
                    else
                    {
                        Console.WriteLine($"[REQ] Page not found : {fullPath}");
                    }

                    return BuildHttpResponse("404 Not Found", new Dictionary<string, string>
                    {
                        ["Content-Type"] = "text/html; charset=UTF-8",
                        ["Connection"] = "close",
                        ["Server"] = "Webii/1.0"
                    }, Return404());

                //break;
                case REQType.POST:
                    break;
                default:
                    break;
            }
            return "";
        }
       
        string ReturnPageByPath(string path)
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
                    _ => "application/octet-stream" // Domyślny typ dla nieznanych plików
                };

                if (contentType.StartsWith("image/")) // Obsługa obrazów
                {
                    byte[] imageData = server.FileRam.GetBytes(path);
                    if (imageData != null)
                    {
                        var responseHeaders = new Dictionary<string, string>
                        {
                            ["Content-Type"] = contentType,
                            ["Content-Length"] = imageData.Length.ToString(),
                            ["Connection"] = "close",
                            ["Server"] = "Webii/1.0"
                        };

                        return BuildHttpResponse("200 OK", responseHeaders, imageData);
                    }
                } else
                if (contentType == "text/html")
                {
                    string htmlContent = server.FileRam.GetText(path);
                    var responseHeaders = new Dictionary<string, string>
                    {
                        ["Content-Type"] = contentType,
                        ["Content-Length"] = Encoding.UTF8.GetByteCount(htmlContent).ToString(),
                        ["Connection"] = "close",
                        ["Server"] = "Webii/1.0"
                    };

                    return BuildHttpResponse("200 OK", responseHeaders, htmlContent);
                }
                
            }
            else
            {
                Console.WriteLine($"[ERR] File not found: {path}");
            }
                return null;
        }
        private string BuildHttpResponse(string status, Dictionary<string, string> headers, byte[] body)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {status}");

            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            sb.AppendLine(); // Pusta linia oddzielająca nagłówki od ciała
            return sb.ToString() + Encoding.UTF8.GetString(body);
        }

        private string BuildHttpResponse(string status, Dictionary<string, string> headers, string body)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {status}");

            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            sb.AppendLine(); // Pusta linia oddzielająca nagłówki od ciała
            sb.Append(body);

            return sb.ToString();
        }
       
        public void Set404Page(string path)
        {
            page404 = path;
        }

        string Return404()
        {
            if (string.IsNullOrEmpty(page404))
            {
                return "<h1>404 Not Found</h1><p>The requested resource was not found on this server.</p>";
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
