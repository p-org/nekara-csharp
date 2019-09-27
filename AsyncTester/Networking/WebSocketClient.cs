using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Newtonsoft.Json;

namespace AsyncTester
{
    // Wrapping the native ClientWebSocket class to provide a different high-level interface
    class WebSocketClient : JsonP2P
    {
        private string serverUri;
        private ClientWebSocket socket;
        public event Action<string> onMessage;

        public WebSocketClient(string serverUri) : base()
        {
            this.serverUri = serverUri;
            this.socket = new ClientWebSocket();

            this.socket.ConnectAsync(new Uri(this.serverUri), CancellationToken.None)
                .ContinueWith(prev =>
                {
                    Listen();
                });
        }

        private void Listen()
        {
            var socketDestroyer = new CancellationTokenSource();
            var buffer = new byte[8192];
            Helpers.AsyncTaskLoop(() =>
            {
                if (this.socket.State == WebSocketState.Open)
                {
                    return this.socket.ReceiveAsync(new ArraySegment<byte>(buffer), socketDestroyer.Token)
                        .ContinueWith(prev =>
                        {
                            // Spawning a new task to make the message handler "non-blocking"
                            // TODO: Errors thrown inside here will become silent, so that needs to be handled
                            // Also, now that the single execution flow is broken, the requests are under race conditions
                            Task.Run(() => {

                                try
                                {
                                    string payload = Encoding.UTF8.GetString(buffer, 0, prev.Result.Count);
                                    try
                                    {
                                        this.HandleMessage(payload);
                                    }
                                    catch (Exception ex) when (ex is UnexpectedMessageException || ex is ServerThrownException)
                                    {
                                        if (this.onMessage != null)
                                        {
                                            this.onMessage(payload);
                                        }
                                    }
                                }
                                catch (AggregateException ae)
                                {
                                    Console.WriteLine(ae);
                                    ae.Handle(e =>
                                    {
                                        if (e is WebSocketException)
                                        {
                                            Console.WriteLine("!!! WebSocketException - Connection Closed");
                                            Console.WriteLine("!!! If this was unexpected, inspect the exception object here");
                                            socketDestroyer.Cancel();
                                            return true;
                                        }
                                        else
                                        {
                                            Console.WriteLine("!!! Unexpected Exception: {0}", e);
                                            socketDestroyer.Cancel();
                                            return false;
                                        }
                                    });
                                }

                            });
                        });
                }
                else
                {
                    Console.WriteLine("!!! WebSocket Connection Dropped!");
                    socketDestroyer.Cancel();
                    return Task.CompletedTask;
                }
            }, socketDestroyer.Token);
        }

        public override Task Send(string recipient, string payload)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(payload);
            return this.socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
