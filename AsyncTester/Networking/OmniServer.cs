using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace AsyncTester.Core
{
    // This is the main "host" object that is created by the server process.
    // It does not contain the testing logic - that resides in the TestingService
    // (the actual tester should perhaps call into the existing psharp tester)
    // The transport mechanism is transparent, and this object only exposes the
    // high-level, protocol-agnostic interface.
    public class OmniServer
    {
        private OmniServerConfiguration config;
        private Dictionary<string, RemoteMethodAsync> remoteMethods;

        public OmniServer(OmniServerConfiguration config)
        {
            this.config = config;
            this.remoteMethods = new Dictionary<string, RemoteMethodAsync>();

            // Initialize the testing service before setting up the transport
            // (if it is IPC, it will be initialized differently)
            
            /*if (this.config.transport != Transport.IPC)
            {
                this.service = new TestingService();
                RegisterRemoteMethods(this.service);
            }*/

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
                default: throw new TesterConfigurationException();
            }
        }

        // for registering message handlers that are methods of objects (these method are probably stateful)
        private void RegisterRemoteMethod(string name, object instance, MethodInfo method)
        {
            if (method.ReturnType == typeof(Task<JToken>))
            {
                this.remoteMethods[name] = args =>
                {
                    // Console.WriteLine("    Invoking Remote Method: Task<JToken> {0} ({1})", name, String.Join(",", args.Select(t => t.ToString())));
                    return (Task<JToken>)method.Invoke(instance, args);
                };
            }
            else if (method.ReturnType == typeof(void))
            {
                this.remoteMethods[name] = args =>
                {
                    // Console.WriteLine("    Invoking Remote Method: void {0} ({1})", name, String.Join(",", args.Select(t => t.ToString())) );
                    method.Invoke(instance, args);
                    // method.Invoke(instance, new object[] { args[0].ToObject<int>() });
                    return Task<JToken>.FromResult(JToken.FromObject(0));
                };
            }
            else
            {
                this.remoteMethods[name] = args =>
                {
                    // Console.WriteLine("    Invoking Remote Method: {0} ({1})", name, String.Join(",", args.Select(t => t.ToString())));
                    return Task<JToken>.FromResult(JToken.FromObject(method.Invoke(instance, args)));
                };
            }
            Console.WriteLine("    Registered Remote Method: {0}.{1}", instance.GetType().Name, name);
        }

        // for registering message handlers that are lambda functions (these method are probably stateless)
        public void RegisterRemoteMethod(string name, Func<object, Task<JToken>> handler)
        {
            // wrapping it to print some info
            // it can actually be directly assigned like: new RemoteMethodAsync(handler);
            RemoteMethodAsync method = new RemoteMethodAsync(kwargs => {
                Console.WriteLine("    Invoking Stateless Remote Method {0} ({1})", name, kwargs);
                return handler(kwargs);
            });
            this.remoteMethods[name] = method;
            Console.WriteLine("    Registered Remote Method: {0}", name);
        }

        // for registering a "service" object - i.e., has methods decorated with RemoteMethodAttribute
        public void RegisterService(object service)
        {
            // register the test service API
            var methods = service.GetType().GetMethods()
                    .Where(m => m.GetCustomAttributes(false).OfType<RemoteMethodAttribute>().Any())
                    .ToDictionary(a => a.Name);
            foreach (var item in methods)
            {
                this.RegisterRemoteMethod(item.Key, service, item.Value);
            }
        }

        // this method is called internally by the main message listener loop
        private Task<ResponseMessage> HandleRequest(RequestMessage message)
        {
            Console.WriteLine("--> Client Request: {0} ({1})", message.func, message.args);

            // this is a "meta" remote method, mostly for testing; should be removed later
            if (message.func == "echo")
            {
                return Task.FromResult(message.CreateResponse("Tester-Server", message.args));
            }
            else if (this.remoteMethods.ContainsKey(message.func))
            {
                return this.remoteMethods[message.func](message.args.ToArray())
                    .ContinueWith(prev => {
                        Console.WriteLine("    ... responding to {0}", message.func);
                        if (prev.Result != null) return message.CreateResponse("Tester-Server", prev.Result);
                        else return message.CreateResponse("Tester-Server", new JValue("OK"));
                    });
            }
            else
            {
                return Task.FromResult(message.CreateResponse("Tester-Server", new JValue("ERROR: Could not understand func " + message.func)));
            }
        }

        private void SetupTransportIPC()
        {
            // Create and register the IPC channel
            IpcServerChannel channel = new IpcServerChannel("tester");
            ChannelServices.RegisterChannel(channel, false);

            // Expose an object -- an interface to the service
            RemotingConfiguration.RegisterWellKnownServiceType(typeof(TestingService), "service", WellKnownObjectMode.Singleton);

            // Wait for calls
            Console.WriteLine("... Tester Server Listening on IPC Channel: " + channel.GetChannelUri());
        }

        private void SetupTransportHTTP()
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
}
