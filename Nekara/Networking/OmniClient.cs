using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
//using System.Runtime.Remoting;
//using System.Runtime.Remoting.Channels;
//using System.Runtime.Remoting.Channels.Ipc;
using Newtonsoft.Json.Linq;

namespace Nekara.Networking
{
    public class OmniClient : IClient, IDisposable
    {
        public OmniClientConfiguration config;
        private Func<string, JToken[], (Task<JToken>, CancellationTokenSource)> _sendRequest;    // delegate method to be implemented by differnet transport mechanisms
        private Action<string, RemoteMethodAsync> _addRemoteMethod;    // delegate method to be implemented by differnet transport mechanisms
        private Action _dispose;
        private Task _readyFlag;

        public Task ReadyFlag { get { return _readyFlag; } }
        
        // private Func<string, Task> Subscribe;            // using topic-based Publish-Subscribe
        // private Func<string, string, Task> Publish;      // using topic-based Publish-Subscribe

        public OmniClient(OmniClientConfiguration config)
        {
            this.config = config;

            // Depending on the transport, create the appropriate communication interface
            switch (this.config.Transport)
            {
                case Transport.IPC:
                    SetupTransportIPC();
                    break;
                case Transport.HTTP:
                    SetupTransportHTTP();
                    break;
                case Transport.GRPC:
                    SetupTransportGRPC();
                    break;
                case Transport.WS:
                    SetupTransportWS();
                    break;
                case Transport.TCP:
                    SetupTransportTCP();
                    break;
                default: throw new Exception(); // TODO: make a proper exception later
            }

            // __testStart();
        }

        private void SetupTransportIPC()
        {
            throw new NotImplementedException();
/*

            // Create and register the IPC channel
            IpcClientChannel channel = new IpcClientChannel();
            ChannelServices.RegisterChannel(channel, false);

            // Fetch the proxy object -- an interface to the service
            RemotingConfiguration.RegisterWellKnownClientType(typeof(NekaraServer), "ipc://tester/service");

            // TestingService service = new TestingService();

            // Assign the appropriate SendRequest method
            this._sendRequest = (func, args) =>
            {
                // TODO: this function is incomplete - work on it later
                return ( Task.FromResult(JValue.Parse("true")), new CancellationTokenSource() );
            };

            this._addRemoteMethod = (string func, RemoteMethodAsync handler) =>
            {

            };

            this._dispose = () => {
                
            };
*/
        }

        private void SetupTransportHTTP()
        {
            HttpClient client = new HttpClient("http://" + this.config.serviceHost + ":" + this.config.servicePort.ToString() + "/");

            // Assign the appropriate SendRequest method
            this._sendRequest = (func, args) =>
            {
                if (this.config.PrintVerbosity > 1) Console.WriteLine("\n<-- Requesting {0} ({1})", func, String.Join(", ", args.Select(arg => arg.ToString())));
                var tcs = new TaskCompletionSource<JToken>();
                var cts = new CancellationTokenSource();

                // It is important that we use the default Task scheduler to make this request work correctly with arbitrary frameworks.
                // For instance, without specifying the default task scheduler, this request will not be able to receive a response in orleans,
                // as described in: http://dotnet.github.io/orleans/Documentation/grains/external_tasks_and_grains.html
                // The orleans task scheduler will enforce a single-threaded execution model and the receiver IO will be blocked
                // see also: https://devblogs.microsoft.com/pfxteam/task-run-vs-task-factory-startnew/
                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        var message = new RequestMessage("Tester-Client", "Tester-Server", func, args);
                        var payload = message.Serialize();

                        var result = await client.Post("rpc/", payload, cts.Token);
                        var resp = ResponseMessage.Deserialize(result);
                        if (this.config.PrintVerbosity > 1) Console.WriteLine("\n--> Got Response to {0} {1}\t[{2}({3})]", func, String.Join(", ", args.Select(arg => arg.ToString())), resp.responseTo, resp.error);

                        if (cts.Token.IsCancellationRequested) tcs.SetCanceled();
                        else
                        {
                            if (resp.error) tcs.SetException(Exceptions.DeserializeServerSideException(resp.data));
                            else tcs.SetResult(resp.data);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is TaskCanceledException)
                        {
                            Console.WriteLine("\n!!! [OmniClient._sendRequest] Canceled {0} !!!", Helpers.MethodInvocationString(func, args));
                            tcs.SetCanceled();
                            return;
                        }
                        Console.WriteLine("\n!!! [OmniClient._sendRequest] UNEXPECTED EXCEPTION {0} !!!", ex.GetType().Name);
                        tcs.SetException(ex);
                    }

                }, cts.Token, TaskCreationOptions.None, TaskScheduler.Default);

                return (tcs.Task, cts);
            };

            this._addRemoteMethod = (string func, RemoteMethodAsync handler) =>
            {

            };

            this._dispose = () => {
                client.Dispose();
            };

            this._readyFlag = client.ReadyFlag;
        }

        private void SetupTransportGRPC()
        {
            throw new NotImplementedException();
        }

        private void SetupTransportWS()
        {
            WebSocketClient client = new WebSocketClient("ws://localhost:8080/ws/");
            /*client.onMessage += (string data) =>
            {
                Console.WriteLine("WSC.onMessage triggered: {0}", data);
            };*/

            // Assign the appropriate SendRequest method
            this._sendRequest = (string func, JToken[] args) => client.Request("Tester-Server", func, args);

            this._addRemoteMethod = (string func, RemoteMethodAsync handler) => client.RegisterRemoteMethod(func, handler);

            this._dispose = () => {
                client.Dispose();
            };

            this._readyFlag = client.ReadyFlag;
        }

        private void SetupTransportTCP()
        {
            throw new NotImplementedException();
        }

        // overloading the main SendRequest method to deal with variadic arguments
        public (Task<JToken>, CancellationTokenSource) SendRequest(string func)
        {
            return this._sendRequest(func, new JToken[] { });
        }

        public (Task<JToken>, CancellationTokenSource) SendRequest(string func, JArray args)
        {
            return this._sendRequest(func, args.ToArray<JToken>());
        }

        public (Task<JToken>, CancellationTokenSource) SendRequest(string func, params JToken[] args)
        {
            return this._sendRequest(func, args);
        }

        public (Task<JToken>, CancellationTokenSource) SendRequest(string func, params bool[] args)
        {
            return this._sendRequest(func, args.Select(x => JToken.FromObject(x)).ToArray());
        }

        public (Task<JToken>, CancellationTokenSource) SendRequest(string func, params int[] args)
        {
            return this._sendRequest(func, args.Select(x => JToken.FromObject(x)).ToArray());
        }

        public (Task<JToken>, CancellationTokenSource) SendRequest(string func, params string[] args)
        {
            return this._sendRequest(func, args.Select(x => JToken.FromObject(x)).ToArray());
        }

        public void AddRemoteMethod(string func, RemoteMethodAsync handler)
        {
            this._addRemoteMethod(func, handler);
        }

        // Using this method only during the early stages of development
        // Will be removed after everything is setup
        private void __testStart()
        {

            Helpers.AsyncTaskLoop(() =>
            {
                Console.Write("HTTP: ");
                string input = Console.ReadLine();
                input = Regex.Replace(input, @"[ \t]+", " ");

                string[] tokens = input.Split(' ');
                if (tokens.Length > 0)
                {
                    string cmd = tokens[0].ToLower();
                    if (cmd == "exit" || cmd == "quit") Environment.Exit(0);
                    else if (tokens.Length > 2)
                    {
                        if (cmd == "echo")
                        {
                            var (t, c) = this._sendRequest(cmd, tokens.Skip(1).Select(x => JToken.FromObject(x)).ToArray());
                            return t;
                        }
                        else if (cmd == "do")
                        {
                            string func = tokens[1];
                            JToken[] args = tokens.Skip(2).Select(x => JToken.FromObject(x)).ToArray();
                            var (t, c) = this._sendRequest(func, args);
                            return t;
                        }
                    }
                }

                return Task.Run(() => { });
            });

            // block the main thread here to prevent exiting - as AsyncTaskLoop will return immediately
            while (true)
            {
            }
        }

        public void Dispose()
        {
            this._dispose();
        }
    }
}
