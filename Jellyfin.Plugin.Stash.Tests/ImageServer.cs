using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Stash.Tests
{
    internal sealed class ImageServer : IDisposable
    {
        public static readonly byte[] Picture = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=");

        private readonly HttpListener listener = new HttpListener();
        private readonly Task serving;
        private int requests;

        public ImageServer(string expectedKey)
        {
            var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            this.Url = "http://127.0.0.1:" + ((IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            this.listener.Prefixes.Add(this.Url + "/");
            this.listener.Start();
            this.serving = Task.Run(async () =>
            {
                while (this.listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await this.listener.GetContextAsync();
                    }
                    catch (Exception) when (!this.listener.IsListening)
                    {
                        break;
                    }

                    Interlocked.Increment(ref this.requests);
                    this.LastPath = context.Request.RawUrl;
                    if ((context.Request.Headers["ApiKey"] ?? string.Empty) != expectedKey)
                    {
                        context.Response.StatusCode = 401;
                    }
                    else if (this.Redirect != null)
                    {
                        context.Response.StatusCode = 302;
                        context.Response.RedirectLocation = this.Redirect;
                    }
                    else
                    {
                        context.Response.ContentType = this.MediaType;
                        context.Response.ContentLength64 = this.Oversized ? 21 * 1024 * 1024 : Picture.Length;
                        await context.Response.OutputStream.WriteAsync(Picture);
                    }

                    context.Response.Close();
                }
            });
        }

        public string Url { get; }

        public string LastPath { get; private set; }

        public string Redirect { get; set; }

        public string MediaType { get; set; } = "image/png";

        public bool Oversized { get; set; }

        public int Requests => this.requests;

        public void Dispose()
        {
            this.listener.Stop();
            this.serving.GetAwaiter().GetResult();
            this.listener.Close();
        }
    }
}
