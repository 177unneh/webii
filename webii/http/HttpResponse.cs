using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace webii.http
{
    internal class HttpResponse : IDisposable
    {
        public bool IsBinary { get; set; }
        public byte[] HeaderBytes { get; set; }
        public byte[] Body { get; set; }
        public string TextResponse { get; set; }

        public static HttpResponse CreateTextResponse(string status, Dictionary<string, string> headers, string body)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {status}");

            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            sb.AppendLine();
            sb.Append(body);

            return new HttpResponse
            {
                IsBinary = false,
                TextResponse = sb.ToString()
            };
        }

        public static HttpResponse CreateBinaryResponse(string status, Dictionary<string, string> headers, byte[] body)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {status}");

            foreach (var header in headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            sb.AppendLine();

            return new HttpResponse
            {
                IsBinary = true,
                HeaderBytes = Encoding.UTF8.GetBytes(sb.ToString()),
                Body = body
            };
        }

        public void Dispose()
        {
            if (HeaderBytes != null)
            {
                Array.Clear(HeaderBytes, 0, HeaderBytes.Length);
                HeaderBytes = null;
            }
            if (Body != null)
            {
                Array.Clear(Body, 0, Body.Length);
                Body = null;
            }
            TextResponse = null;
        }
    }
}
