using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nekara.Networking
{
    // Wrapping the native System.Net.Http.HttpClient class
    // to provide a higher level of abstraction
    class HttpClient : IDisposable
    {
        private string serverUri;
        private System.Net.Http.HttpClient client;
        private WebSocketClient websocket;
        public readonly Task ReadyFlag;

        public HttpClient(string serverUri)
        {
            this.serverUri = serverUri;
            Console.WriteLine("Creating HTTP Client connection to " + serverUri);

            // Create the client
            this.client = new System.Net.Http.HttpClient();

            this.ReadyFlag = Task.CompletedTask;
        }

        public void Dispose()
        {
            this.client.Dispose();
        }

        public async Task<Object> Get(string path)
        {
            var tcs = new TaskCompletionSource<Object>();
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                string responseBody = await this.client.GetStringAsync(this.serverUri + path);

                Console.WriteLine(responseBody);

                tcs.SetResult(responseBody);
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }

            return tcs.Task;
        }

        public async Task<string> Post(string path, string payload)
        {
            return await this.Post(path, payload, CancellationToken.None);
        }

        public async Task<string> Post(string path, string payload, CancellationToken token)
        {
            string responseBody;

            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                // Console.WriteLine("    ... POST {0}", this.serverUri + path);
                var httpContent = new System.Net.Http.StringContent(payload, Encoding.UTF8, "application/json");
                System.Net.Http.HttpResponseMessage response = await this.client.PostAsync(this.serverUri + path, httpContent, token);
                response.EnsureSuccessStatusCode();
                responseBody = await response.Content.ReadAsStringAsync();
                //Console.WriteLine(responseBody);
            }
            catch (System.Net.Http.HttpRequestException e)
            {
                Console.WriteLine("\nUnexpected Server Error!");
                Console.WriteLine("Message :\n{0} ", e.Message);
                responseBody = "";
            }
            return responseBody;
        }
    }
}
