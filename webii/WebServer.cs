using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace webii
{
    public class WebServer
    {
        public bool Http;
        public int Port;
        public string Host;
        Iwwwhandler? handler;
        public WebServer(bool UseHttps = false,int port = 80,string host = "localhost")
        {
            Http = !UseHttps;
            Port = port;
            Host = host;
            if (Http)
            {
                handler = new http.httphandler(this);
            }
            else
            {
                //handler = new https.httpshandler(this);
            }
        }

    }
}
