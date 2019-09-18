using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Ipc;
using System.Web;
using System.Net;
using System.Net.Http;

namespace AsyncTester
{
    public enum Mode { IPC, HTTP, TCP }  // AsyncTester could be used in a network setting. Easily switch between modes with this config flag.

    public class TesterConfiguration
    {
        private Mode _mode;

        public TesterConfiguration()
        {
            this._mode = Mode.IPC;
        }

        public Mode mode
        {
            get { return this._mode; }
            set { this._mode = value; }
        }
    }

    class TesterServer
    {
        private TesterConfiguration config;

        public TesterServer(TesterConfiguration config)
        {
            this.config = config;

            // Initialize a test runtime
            Runtime runtime = new Runtime(new RuntimeConfiguation());

            // Depending on the mode, create the appropriate communication interface
            if (this.config.mode == Mode.IPC)
            {
                // Create and register the IPC channel
                IpcServerChannel channel = new IpcServerChannel("tester");
                ChannelServices.RegisterChannel(channel, false);

                // Expose an object -- an interface to the service
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(TesterService), "service", WellKnownObjectMode.Singleton);

                // Wait for calls
                Console.WriteLine("... Tester Server Listening on IPC Channel: " + channel.GetChannelUri());
                
            }
        }
    }

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

    public class TesterClient
    {
        private TesterConfiguration config;

        public TesterClient(TesterConfiguration config)
        {
            this.config = config;

            // Depending on the mode, create the appropriate communication interface
            if (this.config.mode == Mode.IPC)
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
        }
    }
}
