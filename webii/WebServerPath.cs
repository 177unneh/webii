using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using webii.http;

namespace webii
{
    public class WebServerPath
    {
        private readonly WebServer _server;
        private readonly string _path;
        private readonly string _method;

        public WebServerPath(WebServer server, string path, string method)
        {
            _server = server;
            _path = path;
            _method = method;
        }

        // Operator + dla składni z += (kompilator automatycznie utworzy +=)
        public static WebServerPath operator +(WebServerPath path, Func<string, Dictionary<string, string>, HttpResponse> handler)
        {
            if (path._method == "POST")
                path._server.PostHandlers[path._path] = handler;
            else if (path._method == "PUT")
                path._server.PutHandlers[path._path] = handler;

            return path;
        }

        // Alternatywnie, możesz użyć metody
        public WebServerPath Handler(Func<string, Dictionary<string, string>, HttpResponse> handler)
        {
            if (_method == "POST")
                _server.PostHandlers[_path] = handler;
            else if (_method == "PUT")
                _server.PutHandlers[_path] = handler;

            return this;
        }
    }
}