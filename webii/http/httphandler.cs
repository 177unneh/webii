using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace webii.http
{
    internal class httphandler : IDisposable, Iwwwhandler
    {
        WebServer server;
        HttpListener HttpListener;
        Thread ListeningThread;
        public httphandler(WebServer server)
        {
            this.server = server;
            HttpListener = new HttpListener();
            HttpListener.Prefixes.Add("http://"+server.Host+":"+server.Port);

        }

        public void Dispose()
        {
            //throw new NotImplementedException();
            HttpListener.Stop();
            HttpListener.Close();
            HttpListener = null;
        }

        public void Start()
        {
            HttpListener.Start();
            ListeningThread = new Thread(() =>
            {

            });
        }

        public void Stop()
        {
            HttpListener.Stop();
        }

        void Listening()
        {
            while (true)
            {

            }
        }
    }
}
