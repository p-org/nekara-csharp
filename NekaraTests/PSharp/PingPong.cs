using Microsoft.PSharp;
using Nekara.Client;

namespace Nekara.Tests.PSharp
{
    class PingPong
    {
        [TestMethod]
        public static void Run()
        {
            var configuration = Configuration.Create().WithVerbosityEnabled();
            var runtime = PSharpTestRuntime.Create(configuration);

            runtime.CreateMachine(typeof(NetworkEnvironment));
        }
    }

    /// <summary>
    /// This machine acts as a test harness. It models a network environment,
    /// by creating a 'Server' and a 'Client' machine.
    /// </summary>
    internal class NetworkEnvironment : Machine
    {
        /// <summary>
        /// Each P# machine declares one or more machine states (or simply states).
        ///
        /// One of the states must be declared as the initial state using the 'Start'
        /// attribute. When the machine gets constructed, it will transition the
        /// declared initial state.
        [Start]
        ///
        /// A P# machine state can declare one or more action. This state declares an
        /// 'OnEntry' action, which executes the 'InitOnEntry' method when the machine
        /// transitions to the 'Init' state. Only one 'OnEntry' action can be declared
        /// per machine state.
        [OnEntry(nameof(InitOnEntry))]
        /// </summary>
        class Init : MachineState { }

        /// <summary>
        /// An action that executes (asynchronously) when the 'NetworkEnvironment' machine
        /// transitions to the 'Init' state.
        /// </summary>
        void InitOnEntry()
        {
            // Creates (asynchronously) a server machine.
            var server = this.CreateMachine(typeof(PingPongServer));
            // Creates (asynchronously) a client machine, and passes the
            // 'Config' event as payload. 'Config' contains a reference
            // to the server machine.
            this.CreateMachine(typeof(PingPongClient), new PingPongClient.Config(server));
        }
    }

    /// <summary>
    /// A P# machine that models a simple client.
    ///
    /// It sends 'Ping' events to a server, and handles received 'Pong' event.
    /// </summary>
    internal class PingPongClient : Machine
    {
        /// <summary>
        /// Event declaration of a 'Config' event that contains payload.
        /// </summary>
        internal class Config : Event
        {
            /// <summary>
            /// The payload of the event. It is a reference to the server machine
            /// (send by the 'NetworkEnvironment' machine upon creation of the client).
            /// </summary>
            public MachineId Server;

            public Config(MachineId server)
            {
                this.Server = server;
            }
        }

        /// <summary>
        /// Event declaration of a 'Unit' event that does not contain any payload.
        /// </summary>
        internal class Unit : Event { }

        /// <summary>
        /// Event declaration of a 'Ping' event that contains payload.
        /// </summary>
        internal class Ping : Event
        {
            /// <summary>
            /// The payload of the event. It is a reference to the client machine.
            /// </summary>
            public MachineId Client;

            public Ping(MachineId client)
            {
                this.Client = client;
            }
        }

        /// <summary>
        /// Reference to the server machine.
        /// </summary>
        MachineId Server;

        /// <summary>
        /// A counter for ping-pong turns.
        /// </summary>
        int Counter;

        [Start]
        [OnEntry(nameof(InitOnEntry))]
        class Init : MachineState { }

        void InitOnEntry()
        {
            // Receives a reference to a server machine (as a payload of
            // the 'Config' event).
            this.Server = (this.ReceivedEvent as Config).Server;
            this.Counter = 0;

            // Notifies the P# runtime that the machine must transition
            // to the 'Active' state when 'InitOnEntry' returns.
            this.Goto<Active>();
        }

        /// <summary>
        [OnEntry(nameof(ActiveOnEntry))]
        /// The 'OnEventDoAction' action declaration will execute (asynchrously)
        /// the 'SendPing' method, whenever a 'Pong' event is dequeued while the
        /// client machine is in the 'Active' state.
        [OnEventDoAction(typeof(PingPongServer.Pong), nameof(SendPing))]
        /// </summary>
        class Active : MachineState { }

        void ActiveOnEntry()
        {
            SendPing();
        }

        void SendPing()
        {
            this.Counter++;

            // Sends (asynchronously) a 'Ping' event to the server that contains
            // a reference to this client as a payload.
            this.Send(this.Server, new Ping(this.Id));

            this.Logger.WriteLine("Client request: {0} / 5", this.Counter);

            if (this.Counter == 5)
            {
                // If 5 'Ping' events where sent, then raise the special event 'Halt'.
                //
                // Raising an event, notifies the P# runtime to execute the event handler
                // that corresponds to this event in the current state, when 'SendPing'
                // returns.
                //
                // In this case, when the machine handles the special event 'Halt', it
                // will terminate the machine and release any resources. Note that the
                // 'Halt' event is handled automatically, the user does not need to
                // declare an event handler in the state declaration.
                this.Raise(new Halt());
            }
        }
    }

    /// <summary>
    /// A P# machine that models a simple server.
    ///
    /// It receives 'Ping' events from a client, and responds with a 'Pong' event.
    /// </summary>
    internal class PingPongServer : Machine
    {
        /// <summary>
        /// Event declaration of a 'Pong' event that does not contain any payload.
        /// </summary>
        internal class Pong : Event { }

        [Start]
        /// <summary>
        /// The 'OnEventDoAction' action declaration will execute (asynchrously)
        /// the 'SendPong' method, whenever a 'Ping' event is dequeued while the
        /// server machine is in the 'Active' state.
        [OnEventDoAction(typeof(PingPongClient.Ping), nameof(SendPong))]
        /// </summary>
        class Active : MachineState { }

        void SendPong()
        {
            // Receives a reference to a client machine (as a payload of
            // the 'Ping' event).
            var client = (this.ReceivedEvent as PingPongClient.Ping).Client;
            // Sends (asynchronously) a 'Pong' event to the client.
            this.Send(client, new Pong());
        }
    }
}
