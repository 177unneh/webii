using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace webii.http
{
    using System;
    using System.Collections.Generic;
    using System.Text;

   
        public class HttpResponse : IDisposable
        {
            public enum StatusType
            {
                Sucess200,Notfound404,ServerError500,NotImplemented501, ServiceUnavailable503
        }
            public bool IsBinary { get; set; }
            public byte[] HeaderBytes { get; set; }
            public byte[] Body { get; set; }
            public string TextResponse { get; set; }

            // Typowe nagłówki mają około 200-300 bajtów
            private const int InitialHeaderCapacity = 512;

            public static HttpResponse CreateTextResponse(string status, Dictionary<string, string> headers, string body)
            {
                // Prealokuj miejsce na StringBuilder dla lepszej wydajności
                var sb = new StringBuilder(InitialHeaderCapacity + (body?.Length ?? 0));
                sb.AppendLine($"HTTP/1.1 {status}");

                foreach (var header in headers)
                {
                    sb.Append(header.Key).Append(": ").AppendLine(header.Value);
                }

                sb.AppendLine();

                if (body != null)
                {
                    sb.Append(body);
                }

                return new HttpResponse
                {
                    IsBinary = false,
                    TextResponse = sb.ToString()
                };
            }

            public static HttpResponse CreateBinaryResponse(string status, Dictionary<string, string> headers, byte[] body)
            {
                var sb = new StringBuilder(InitialHeaderCapacity);
                sb.AppendLine($"HTTP/1.1 {status}");

                foreach (var header in headers)
                {
                    sb.Append(header.Key).Append(": ").AppendLine(header.Value);
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
                    // Czyścimy tylko dane, nie całą tablicę
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
