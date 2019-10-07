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
    class WebSocketClient : JsonP2P, IDisposable
    {
        private string serverUri;
        private ClientWebSocket socket;
        public event Action<string> onMessage;
        private object sendLock;
        private object receiveLock;

        public WebSocketClient(string serverUri) : base()
        {
            this.serverUri = serverUri;
            this.socket = new ClientWebSocket();
            this.sendLock = new object();
            this.receiveLock = new object();

            this.socket.ConnectAsync(new Uri(this.serverUri), CancellationToken.None)
                .ContinueWith(prev =>
                {
                    Listen();
                });
        }

        private void Listen()
        {
            var socketDestroyer = new CancellationTokenSource();
            var buffer = new byte[65536];
            Helpers.AsyncTaskLoop(() =>
            {
                /*try
                {
                    Monitor.Enter(receiveLock);*/
                    if (this.socket.State == WebSocketState.Open)
                    {
                        return this.socket.ReceiveAsync(new ArraySegment<byte>(buffer), socketDestroyer.Token)
                            .ContinueWith(prev =>
                            {
                                //Monitor.Exit(receiveLock);

                                // Spawning a new task to make the message handler "non-blocking"
                                // TODO: Errors thrown inside here will become silent, so that needs to be handled
                                // Also, now that the single execution flow is broken, the requests are under race conditions
                                Task.Run(() => {
                                    // Console.WriteLine("  ... handling message on thread {0}", Thread.CurrentThread.ManagedThreadId);
                                    try
                                    {
                                        string payload = Encoding.UTF8.GetString(buffer, 0, prev.Result.Count);
                                        // Console.WriteLine("  ... raw message: {0}", payload);
                                        try
                                        {
                                            this.HandleMessage(payload);
                                        }
                                        catch (Exception ex) when (ex is UnexpectedMessageException || ex is ServerThrownException)
                                        {
                                            Console.WriteLine(ex);
                                            if (this.onMessage != null)
                                            {
                                                this.onMessage(payload);
                                            }
                                        }
                                    }
                                    catch (AggregateException ae)
                                    {
                                    // Console.WriteLine(ae);
                                    foreach (var ie in ae.Flatten().InnerExceptions)
                                        {
                                            Console.WriteLine("Exception -------------------");
                                            Console.WriteLine(ie.Message);
                                            Console.WriteLine(ie.InnerException.Message);
                                            Console.WriteLine(ie.InnerException.StackTrace);
                                            Console.WriteLine("-----------------------------\n");
                                        }

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
                /*}
                finally
                {
                    Monitor.Exit(receiveLock);
                }*/
            }, socketDestroyer.Token);
        }

        public override Task Send(string recipient, string payload)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(payload);
            // We lock the socket because multiple tasks can be racing to use the websocket.
            // The websocket will fail if two tasks try to call client.Send concurrently.
            // We use the low-level Monitor.Enter/Exit because we need to release asynchronously

            try
            {
                Monitor.Enter(sendLock);
                var sendTask = this.socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                sendTask.ContinueWith(t => Monitor.Exit(sendLock));
                return sendTask;
            }
            finally
            {
                Monitor.Exit(sendLock);
            }
        }

        public void Dispose()
        {
            this.socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client Signing Out", CancellationToken.None).Wait();
            this.socket.Dispose();
        }
    }
}
