using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using webii.http;

namespace webii
{
    public class WebServer : IDisposable
    {
        public bool Http;
        public int Port;
        public IPAddress Host;
        Iwwwhandler? handler;
        public enum REQType{
            GET,
            POST,
            PUT,
            OPTIONS
        }
        public FileRam FileRam;
        public string RootDirectory;
        public string publicDirectory;
        private static string _versionNumber;
        private string _versionString;

        public static string VersionNumber
        {
            get
            {
                if (_versionNumber == null)
                {
                    _versionNumber = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                }
                return _versionNumber;
            }
        }

      
        public WebServer(IPAddress ip,bool UseHttps = false, int port = 80, string rootDirectory = null,int mb = 100)
        {
            Http = !UseHttps;
            Port = port;
            Host = ip;
            FileRam = new FileRam(1024*1024*mb);
            if (Http)
            {
                handler = new http.httphandler(this);
            }
            else
            {
                //handler = new https.httpshandler(this);
                throw new NotImplementedException("HTTPS handler is not implemented yet.");
            }


            if (rootDirectory == null)
            {
                // Ustaw katalog na domyślny katalog aplikacji
                rootDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Webii");
            }
            else if (!System.IO.Directory.Exists(rootDirectory))
            {
                throw new ArgumentException("The specified root directory does not exist.", nameof(rootDirectory));
            }
            RootDirectory = rootDirectory;
            if (!System.IO.Directory.Exists(RootDirectory))
            {
                System.IO.Directory.CreateDirectory(RootDirectory);
            }
            Console.WriteLine($"WebServer initialized at {RootDirectory}");
            publicDirectory = Path.Combine(RootDirectory, "public");
            if (!System.IO.Directory.Exists(publicDirectory))
            {
                System.IO.Directory.CreateDirectory(publicDirectory);
            }
        }
        public void Set404Page(string path)
        {
            if(string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                throw new ArgumentException("The specified 404 page path is invalid or does not exist.", nameof(path));
            }
            if (handler == null)
            {
                throw new Exception("WebServer handler is not initialized.");
            }
            handler.Set404Page(path);
        }
        public void Start()
        {
            if (handler == null)
            {
                throw new Exception("WebServer handler is not initialized.");
            }
            handler.Start();
        }


        public Dictionary<string, Func<string, Dictionary<string, string>, HttpResponse>> PostHandlers = new();
        public Dictionary<string, Func<string, Dictionary<string, string>, HttpResponse>> PutHandlers = new();
        public Dictionary<string, Func<string, Dictionary<string, string>, HttpResponse>> GetHandlers = new();

        public void Get(string path, Func<string, Dictionary<string, string>, HttpResponse> handler)
        {
            GetHandlers[path] = handler;
        }

        public WebServerPath Get(string path)
        {
            return new WebServerPath(this, path, "GET");
        }
        // Istniejące metody...

        public void Post(string path, Func<string, Dictionary<string, string>, HttpResponse> handler)
        {
            PostHandlers[path] = handler;
        }

        public void Put(string path, Func<string, Dictionary<string, string>, HttpResponse> handler)
        {
            PutHandlers[path] = handler;
        }
        public WebServerPath Post(string path)
        {
            return new WebServerPath(this, path, "POST");
        }

        public WebServerPath Put(string path)
        {
            return new WebServerPath(this, path, "PUT");
        }

        public void Dispose()
        {
            if (handler != null)
            {
                handler.Dispose();
            }
            handler = null;
            PostHandlers.Clear();
            PutHandlers.Clear();
            GetHandlers.Clear();
            PostHandlers = null;
            PutHandlers = null;
            GetHandlers = null;
            FileRam.Dispose();
        }
    }
}

