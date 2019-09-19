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

namespace AsyncTester
{
    // AsyncTester could be used in a network setting. Easily switch between transports with this config flag.
    //   IPC - Inter-process communication (.net native)
    //   HTTP - HyperText Transport Protocol
    //   TCP - Raw TCP (Transmission Control Protocol)
    //   WS - WebSocket (implemented over HTTP - this has a different communication pattern as the server-side can "push" to the client)
    public enum Transport { IPC, HTTP, TCP, WS }

    public class TesterConfiguration
    {
        private Transport _transport;
        private string host;    // used if Transport == HTTP
        private int port;       // used if Transport == HTTP or TCP

        public TesterConfiguration()
        {
            this._transport = Transport.HTTP;
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

        public TesterServer(TesterConfiguration config)
        {
            this.config = config;

            // Initialize a test runtime
            Runtime runtime = new Runtime(new RuntimeConfiguation());

            // Depending on the transport, create the appropriate communication interface
            if (this.config.transport == Transport.IPC)
            {
                // Create and register the IPC channel
                IpcServerChannel channel = new IpcServerChannel("tester");
                ChannelServices.RegisterChannel(channel, false);

                // Expose an object -- an interface to the service
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(TesterService), "service", WellKnownObjectMode.Singleton);

                // Wait for calls
                Console.WriteLine("... Tester Server Listening on IPC Channel: " + channel.GetChannelUri());
            }
            else if (this.config.transport == Transport.HTTP)
            {
                // Create an HttpServer and bind to network socket
                HttpServer server = new HttpServer("localhost", 8080);

                // Expose the service
                server.Use((Request request, Response response, Action next) => {
                    Console.WriteLine("Received {0} {1}", request.method, request.path);
                    Console.WriteLine(request.body);
                    next();
                });

                server.Post("createTask/", (Request request, Response response, Action next) =>
                {
                    response.Send(200, request.body);
                });

                server.Post("startTask/", (Request request, Response response, Action next) =>
                {
                    response.Send(200, request.body);
                });

                server.Post("endTask/", (Request request, Response response, Action next) =>
                {
                    response.Send(200, request.body);
                });

                server.Post("echo/", (Request request, Response response, Action next) =>
                {
                    response.Send(200, request.body);
                });

                /*
                server.Use((Request request, Response response, Action next) => {
                    response.Send(200, "You said: " + request.body);
                    next();
                });
                */

                server.Listen();

                // Wait for calls
                Console.WriteLine("... Tester Server Listening on HTTP Port: 8080");
            }
        }
    }

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
    }

    // This is the client-side proxy of the tester service.
    // Used when the system is operating in a network setting.
    class TesterServiceProxy
    {
        public HttpClient client;

        public TesterServiceProxy(string serverUri)
        {
            // Create the client
            this.client = new HttpClient(serverUri);
        }
    }

    public class TesterClient
    {
        private TesterConfiguration config;

        public TesterClient(TesterConfiguration config)
        {
            this.config = config;

            // Depending on the transport, create the appropriate communication interface
            if (this.config.transport == Transport.IPC)
            {
                // Create and register the IPC channel
                IpcClientChannel channel = new IpcClientChannel();
                ChannelServices.RegisterChannel(channel, false);

                // Fetch the proxy object -- an interface to the service
                RemotingConfiguration.RegisterWellKnownClientType(typeof(TesterService), "ipc://tester/service");

                TesterService service = new TesterService();
                while (true)
                {
                    Console.WriteLine("Next Seed? ");
                    string input = Console.ReadLine();
                    if (input == "q") break;

                    int seed;

                    try
                    {
                        seed = Int32.Parse(input);
                    }
                    catch (System.FormatException e)
                    {
                        seed = 0;
                    }
                    Console.WriteLine("Using seed: " + seed.ToString());
                    service.GetResult(seed);
                    // Console.WriteLine(service.Count);
                }
            }
            else if (this.config.transport == Transport.HTTP)
            {
                // Create the proxy object -- an interface to the service
                TesterServiceProxy service = new TesterServiceProxy("http://localhost:8080/");

                Helpers.AsyncTaskLoop(() =>
                {
                    Console.Write("HTTP: ");
                    string input = Console.ReadLine();
                    input = Regex.Replace(input, @"[ \t]+", " ");

                    string[] tokens = input.Split(' ');
                    if (tokens.Length > 0)
                    {
                        if (tokens[0] == "exit" || tokens[0] == "quit") Environment.Exit(0);
                        else if (tokens[0].ToUpper() == "GET")
                        {
                            return service.client.Get(tokens[1]);
                        }
                        else if (tokens[0].ToUpper() == "POST")
                        {
                            return service.client.Post(tokens[1], String.Join(" ", tokens.Skip(2)));
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
    }
}
