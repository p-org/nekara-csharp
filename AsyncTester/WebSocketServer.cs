using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncTester
{
    class WebSocketServer
    {
        public Dictionary<string, WebSocket> clients;

        public WebSocketServer()
        {
            this.clients = new Dictionary<string, WebSocket>();
        }

        // This method uses an UNDOCUMENTED object - 
        public string AddClient(HttpListenerWebSocketContext context)
        {
            string socketId = Helpers.RandomString(16);
            var socket = context.WebSocket;            
            var socketDestroyer = new CancellationTokenSource();
            Console.WriteLine("Got {1}! {0}", socketId, socket.ToString());

            byte[] buffer = new byte[8192]; // 8 KB buffer
            Helpers.AsyncTaskLoop(() =>
            {
                if (socket.State == WebSocketState.Open)
                {
                    return socket.ReceiveAsync(new ArraySegment<byte>(buffer), socketDestroyer.Token)
                    .ContinueWith(prev =>
                    {
                        try
                        {
                            var received = prev.Result;
                            Console.WriteLine("WebSocket {0}: {1} {2} {3}", socketId, received.Count, received.MessageType, received.EndOfMessage);
                            if (prev.Result.MessageType == WebSocketMessageType.Close)
                            {
                                Console.WriteLine("Closing WebSocket {0}", socketId);
                                socketDestroyer.Cancel();
                                return socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close OK", CancellationToken.None)
                                    .ContinueWith(_ => this.RemoveClient(socketId));
                            }
                            else
                            {
                                Console.WriteLine("Echoing to WebSocket {0}", socketId);
                                return socket.SendAsync(new ArraySegment<byte>(buffer, 0, prev.Result.Count), prev.Result.MessageType, prev.Result.EndOfMessage, socketDestroyer.Token);
                            }
                        }
                        catch (AggregateException ae)
                        {
                            Console.WriteLine("Exception during async communication with Client {0}\n{1}", socketId, ae);
                            socketDestroyer.Cancel();
                            this.RemoveClient(socketId);
                            return Task.CompletedTask;
                        }
                    }).Unwrap();
                }
                else
                {
                    Console.WriteLine("WebSocket {0} Connection Dropped!", socketId);
                    socketDestroyer.Cancel();
                    this.RemoveClient(socketId);
                    return Task.CompletedTask;
                }
            }, socketDestroyer.Token);

            this.clients.Add(socketId, socket);
            return socketId;
        }

        public void RemoveClient(string socketId)
        {
            var client = this.clients[socketId];
            client.Dispose();
            // client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Close OK", CancellationToken.None)
                //.ContinueWith(prev => client.Dispose());
            this.clients.Remove(socketId);
        }
    }
}
