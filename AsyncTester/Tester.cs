using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
// using System.Web;
using System.Net;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace AsyncTester
{
    // AsyncTester could be used in a network setting. Easily switch between transports with this config flag.
    //   IPC - Inter-process communication (.net native)
    //   HTTP - HyperText Transport Protocol (HTTP/1)
    //   GRPC - gRPC Remote Procedure Calls (implemented over HTTP/2)
    //   WS - WebSocket (implemented over HTTP - this has a different communication pattern as the server-side can "push" to the client)
    //   TCP - Raw TCP (Transmission Control Protocol)
    public enum Transport { IPC, HTTP, GRPC, WS, TCP }

    public delegate Task<object> RemoteMethodAsync(object kwargs);

    public class RemoteMethodAttribute : Attribute
    {
        public string name;
        public string description;
    }

    public class TesterConfiguration
    {
        private Transport _transport;
        private string host;    // used if Transport == HTTP
        private int port;       // used if Transport == HTTP or TCP

        public TesterConfiguration()
        {
            this._transport = Transport.WS;
        }

        public Transport transport
        {
            get { return this._transport; }
            set { this._transport = value; }
        }
    }

    // This is the main "host" object that is created by the server process.
    // It does not contain the testing logic - that resides in the TesterService and the Runtime objects
    // (the actual tester should perhaps call into the existing psharp tester)
    class TesterServer
    {
        private TesterConfiguration config;
        private TesterService service;
        private Dictionary<string, RemoteMethodAsync> remoteMethods;

        public TesterServer(TesterConfiguration config)
        {
            this.config = config;
            this.remoteMethods = new Dictionary<string, RemoteMethodAsync>();

            // Initialize a test runtime
            // Runtime runtime = new Runtime(new RuntimeConfiguation());

            // Initialize the testing service before setting up the transport
            // (if it is IPC, it will be initialized differently)
            if (this.config.transport != Transport.IPC)
            {
                this.service = new TesterService();
                RegisterRemoteMethods(this.service);
            }

            // Depending on the transport, create the appropriate communication interface
            switch (this.config.transport)
            {
                case Transport.IPC: SetupTransportIPC();
                    break;
                case Transport.HTTP: SetupTransportHTTP();
                    break;
                case Transport.GRPC: SetupTransportGRPC();
                    break;
                case Transport.WS: SetupTransportWS();
                    break;
                case Transport.TCP: SetupTransportTCP();
                    break;
                default: throw new TesterConfigurationException();
            }
        }

        private Task<ResponseMessage> HandleRequest (RequestMessage message)
        {
            Console.WriteLine("Client Request: {0} ({1})", message.func, message.args);

            // this is a "meta" remote method, mostly for testing; should be removed later
            if (message.func == "echo")
            {
                return Task.FromResult(new ResponseMessage(message.id, message.args));
            }
            else if (this.remoteMethods.ContainsKey(message.func))
            {
                return this.remoteMethods[message.func](message.args)
                    .ContinueWith(prev => {
                        if (prev.Result != null) return new ResponseMessage(message.id, prev.Result.ToString());
                        else return new ResponseMessage(message.id, "OK");
                    });
            }
            else
            {
                return Task.FromResult(new ResponseMessage(message.id, "ERROR: Could not understand func " + message.func));
            }
        }

        private void RegisterRemoteMethods(object service)
        {
            var methods = service.GetType().GetMethods()
                    .Where(m => m.GetCustomAttributes(false).OfType<RemoteMethodAttribute>().Any())
                    .ToDictionary(a => a.Name);
            foreach (var item in methods)
            {
                RemoteMethodAsync method = new RemoteMethodAsync((object kwargs) =>
                {
                    Console.WriteLine("    Invoking Remote Method {0} ({1})", item.Key, kwargs);
                    return (Task<object>) item.Value.Invoke(service, new [] { kwargs });
                });

                this.remoteMethods[item.Key] = method;
            }
        }

        private void SetupTransportIPC ()
        {
            // Create and register the IPC channel
            IpcServerChannel channel = new IpcServerChannel("tester");
            ChannelServices.RegisterChannel(channel, false);

            // Expose an object -- an interface to the service
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(TesterService), "service", WellKnownObjectMode.Singleton);

            // Wait for calls
            Console.WriteLine("... Tester Server Listening on IPC Channel: " + channel.GetChannelUri());
        }

        private void SetupTransportHTTP ()
        {
            // Create an HttpServer and bind to network socket
            HttpServer server = new HttpServer("localhost", 8080);
            
            // Top-level middleware function - just print some things for debugging
            server.Use((Request request, Response response, Action next) => {
                Console.WriteLine("Received {0} {1}", request.method, request.path);
                Console.WriteLine(request.body);
                next();
            });

            // test endpoint
            server.Post("echo/", (Request request, Response response, Action next) =>
            {
                response.Send(200, request.body);
            });

            /* Expose the service */
            server.Post("rpc/", (Request request, Response response, Action next) =>
            {
                RequestMessage message = JsonConvert.DeserializeObject<RequestMessage>(request.body);
                HandleRequest(message)
                .ContinueWith(prev =>
                {
                    ResponseMessage reply = prev.Result;
                    response.Send(200, reply);
                });
            });

            server.Listen();

            // Wait for calls
            Console.WriteLine("... Tester Server Listening on HTTP Port: 8080");
        }

        private void SetupTransportGRPC()
        {

        }

        private void SetupTransportWS()
        {
            // Create a WebSocket Server
            WebSocketServer server = new WebSocketServer("localhost", 8080, "ws/");

            /* Expose the service */
            server.OnNewClient((WebSocketClientHandle client) =>
            {
                client.OnMessage((string data) => {
                    RequestMessage message = JsonConvert.DeserializeObject<RequestMessage>(data);
                    HandleRequest(message)
                    .ContinueWith(prev =>
                    {
                        ResponseMessage reply = prev.Result;
                        client.Send(reply);
                    });
                });
            });

            server.Listen();

            // Wait for calls
            Console.WriteLine("... WebSocket Server Listening on HTTP Port: 8080");
        }

        private void SetupTransportTCP()
        {

        }
    }

    // This object will "connect" the communication mechanism and the service proxy object.
    // The separation between the transport architecture and the logical, abstract model is intentional.
    public class TesterClient
    {
        private TesterConfiguration config;
        private Func<string, string, Task> SendRequest;

        // private Func<string, Task> Subscribe;  // using topic-based Publish-Subscribe
        // private Func<string, string, Task> Publish;    // using topic-based Publish-Subscribe

        public TesterClient(TesterConfiguration config)
        {
            this.config = config;

            // Depending on the transport, create the appropriate communication interface
            switch (this.config.transport)
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
                default: throw new Exception(); // make a proper exception later
            }

            __testStart();
        }

        private void SetupTransportIPC()
        {
            // Create and register the IPC channel
            IpcClientChannel channel = new IpcClientChannel();
            ChannelServices.RegisterChannel(channel, false);

            // Fetch the proxy object -- an interface to the service
            RemotingConfiguration.RegisterWellKnownClientType(typeof(TesterService), "ipc://tester/service");

            TesterService service = new TesterService();

            this.SendRequest = (string func, string args) =>
            {
                // TODO: this function is incomplete - work on it later
                return service.GetResult(0);
            };
        }

        private void SetupTransportHTTP()
        {
            HttpClient client = new HttpClient("http://localhost:8080/");

            // Assign the appropriate Request method
            this.SendRequest = (string func, string args) =>
            {
                return client.Post("rpc/", new RequestMessage(func, args));
            };
        }

        private void SetupTransportGRPC()
        {
            
        }

        private void SetupTransportWS()
        {
            WebSocketClient client = new WebSocketClient("ws://localhost:8080/ws/");
            client.onMessage += (string data) =>
            {
                Console.WriteLine(data);
            };

            // Assign the appropriate Request method
            this.SendRequest = (string func, string args) =>
            {
                return client.Send(new RequestMessage(func, args));
            };
        }

        private void SetupTransportTCP()
        {

        }

        // Using this method only during the early stages of development
        // Will be removed after everything is setup
        private void __testStart ()
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
                            return this.SendRequest(cmd, String.Join(" ", tokens.Skip(1)));
                        }
                        else if (cmd == "do")
                        {
                            string func = tokens[1];
                            string args = String.Join(" ", tokens.Skip(2));
                            return this.SendRequest(func, args);
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
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    class RequestMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string func;

        [DataMember]
        internal string args;

        public RequestMessage(string func, string args)
        {
            this.id = "req-" + Helpers.RandomString(16);
            this.func = func;
            this.args = args;
        }
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    class ResponseMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string responseTo;

        [DataMember]
        internal string data;

        public ResponseMessage(string requestId, string data)
        {
            this.id = "res-" + Helpers.RandomString(16);
            this.responseTo = requestId;
            this.data = data;
        }
    }

    /* The objects below are transport-agnostic and deals only with the user-facing testing API.
     * The only thing related to the transport mechanism is the RemoteMethodAttribute
     */
    
    // This is the service object exposed to the client, and hosted on the server-side
    // The API should be defined on this object.
    class TesterService : MarshalByRefObject
    {
        private int count = 0;
        public int Count {
            get {
                Console.WriteLine("Client asking for count... it is currently " + count.ToString());
                return (count++);
            }
        }

        public Task<int> GetResult(int seed)
        {
            var tcs = new TaskCompletionSource<int>();
            var timer = new Timer((state) =>
            {
                Console.WriteLine("Running Task {0} on Thread {1}", Task.CurrentId, Thread.CurrentThread.ManagedThreadId);
                var rand = new Random(seed);
                tcs.SetResult(rand.Next());
            }, null, 2500, Timeout.Infinite);

            // tcs.Task.Start();
            return tcs.Task;
        }

        [RemoteMethod(name = "CreateTask", description = "Creates a new task")]
        public Task<object> CreateTask(object kwargs)
        {
            return Task.FromResult((object) null);
        }

        [RemoteMethod(name = "StartTask", description = "Signals the start of a given task")]
        public Task<object> StartTask(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "EndTask", description = "Signals the end of a given task")]
        public Task<object> EndTask(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "CreateResource", description = "Notifies the creation of a new resource")]
        public Task<object> CreateResource(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "DeleteResource", description = "Signals the deletion of a given resource")]
        public Task<object> DeleteResource(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "ContextSwitch", description = "Signals the deletion of a given resource")]
        public Task<object> ContextSwitch(object kwargs) {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "BlockedOnResource", description = "")]
        public Task<object> BlockedOnResource(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "SignalUpdatedResource", description = "")]
        public Task<object> SignalUpdatedResource(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "CreateNondetBool", description = "")]
        public Task<object> CreateNondetBool(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "CreateNondetInteger", description = "")]
        public Task<object> CreateNondetInteger(object kwargs)
        {
            return Task.FromResult((object)null);
        }

        [RemoteMethod(name = "Assert", description = "")]
        public Task<object> Assert(object kwargs)
        {
            return Task.FromResult((object)null);
        }
    }

    // This is the client-side proxy of the tester service.
    // Used when the system is operating in a network setting.
    class TesterServiceProxy
    {
        public TesterServiceProxy(string serverUri)
        {
            
        }
    }
}
