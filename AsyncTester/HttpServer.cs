using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

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
    class Request
    {
        private HttpListenerRequest request;

        public Request(HttpListenerRequest request)
        {
            this.request = request;
        }
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

        public void Listen()
        {
            listener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                HandleRequest();
            }
        }

        private void HandleRequest()
        {
            HttpListenerContext context = listener.GetContext();

            Request request = new Request(context.Request);
            Response response = new Response(context.Response);
            Action next = null;

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