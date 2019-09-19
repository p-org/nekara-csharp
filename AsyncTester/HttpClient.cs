using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester
{
    // Wrapping the native System.Net.Http.HttpClient class
    // to provide a higher level of abstraction
    class HttpClient
    {
        private string serverUri;
        private System.Net.Http.HttpClient client;

        public HttpClient(string serverUri)
        {
            this.serverUri = serverUri;

            // Create the client
            this.client = new System.Net.Http.HttpClient();
        }

        public async Task<Object> Get(string path)
        {
            var tcs = new TaskCompletionSource<Object>();
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                // HttpResponseMessage response = await this.client.GetAsync(this.serverUri + path);
                // response.EnsureSuccessStatusCode();
                // string responseBody = await response.Content.ReadAsStringAsync();
                // Above three lines can be replaced with new helper method below
                // string responseBody = await client.GetStringAsync(uri);

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

        public async Task<Object> Post(string path, string payload)
        {
            var tcs = new TaskCompletionSource<Object>();
            // Call asynchronous network methods in a try/catch block to handle exceptions.
            try
            {
                System.Net.Http.HttpResponseMessage response = await this.client.PostAsync(this.serverUri + path, new System.Net.Http.StringContent(payload));
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

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
    }
}
