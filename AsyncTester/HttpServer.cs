using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Reflection;

namespace AsyncTester
{
    delegate void Middleware(Request request, Response response, Action next);

    class Router
    {
        protected List<Middleware> middlewares;

        public Router()
        {
            this.middlewares = new List<Middleware>();
        }

        public void Use(Middleware middleware)
        {
            this.middlewares.Add(middleware);
        }

        public Router Route(string path)
        {
            return new Router();
        }
    }

    // light wrapper around the native HttpListenerRequest class to hide away the low-level details
    // this may not be necessary as the native class is pretty high-level, but keeping it for now in case we need that extra level of indirection.
    class Request
    {
        private HttpListenerRequest request;
        private string _body;

        public Request(HttpListenerRequest request)
        {
            this.request = request;

            if (request.HasEntityBody)
            {
                Stream body = request.InputStream;
                Encoding encoding = request.ContentEncoding;
                StreamReader reader = new StreamReader(body, encoding);
                if (request.ContentType != null)
                {
                    Console.WriteLine("Client data content type {0}", request.ContentType);
                }
                Console.WriteLine("Client data content length {0}", request.ContentLength64);

                // Console.WriteLine("Start of client data:");
                // Convert the data to a string and display it on the console.
                this._body = reader.ReadToEnd();
                // Console.WriteLine(s);
                // Console.WriteLine("End of client data:");
                body.Close();
                reader.Close();
            }
            else this._body = "";
        }

        public string method { get { return this.request.HttpMethod; } }
        public string path { get { return this.request.Url.PathAndQuery; } }
        public string body { get { return this._body; } }
        public CookieCollection cookies { get { return this.request.Cookies; } }
    }

    // light wrapper around the native HttpListenerResponse class to hide away the low-level details
    class Response
    {
        private HttpListenerResponse response;

        public Response(HttpListenerResponse response)
        {
            this.response = response;
        }

        public void Send(int statusCode, string payload)
        {
            // Construct a response.
            // string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(payload);

            // Get a response stream and write the response to it.
            this.response.ContentLength64 = buffer.Length;
            System.IO.Stream output = this.response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
            // listener.Stop();
        }
    }

    // This is an HTTP Server providing an asynchronous API like Express.js
    // It should run strictly on a single thread, otherwise things like
    // session data will be under race condition.
    class HttpServer : Router
    {
        private string host;
        private int port;
        private HttpListener listener;

        public HttpServer(string host, int port) : base()
        {
            this.host = host;
            this.port = port;

            this.listener = new HttpListener();
            this.listener.Prefixes.Add("http://" + host + ":" + port.ToString() + "/");
        }

        // Listen returns immediately, and the "listening" is done asynchronously via Task loop.
        public void Listen()
        {
            listener.Start();
            Console.WriteLine("Listening...");

            Helpers.AsyncLoop(HandleRequest);

            // while (true)
            // {
            // HandleRequest();
            // }
        }

        private void HandleRequest()
        {
            HttpListenerContext context = listener.GetContext();

            Request request = new Request(context.Request);
            Response response = new Response(context.Response);
            Action next = null;

            // Get some information about the request


            int current = 0;
            next = () => {
                if (current < this.middlewares.Count)
                {
                    Middleware handler = this.middlewares[current];
                    current++;
                    handler(request, response, next);
                }
            };

            next();
        }
    }
}