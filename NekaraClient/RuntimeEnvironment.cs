using System;
using System.Diagnostics;
using Nekara.Networking;

namespace Nekara.Client
{
    // This is a global singleton that gets updated dynamically during runtime
    public static class RuntimeEnvironment
    {
        public static bool DebugMode = false;
        public static NekaraClient Client { get; set; }

        public static DateTime StartedAt = DateTime.Now;

        static RuntimeEnvironment()
        {
            // Debug
            if (DebugMode)
            {
                /*AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) =>
                {
                    Debug.WriteLine(eventArgs.Exception.ToString());
                };*/
                Debugger.Launch();
            }

            // client-side socket
            OmniClient socket = new OmniClient(new OmniClientConfiguration(Transport.HTTP, "localhost", 8080));

            // testing service proxy object;uses the socket to communicate to the actual testing service
            Client = new NekaraClient(socket);

            socket.ReadyFlag.Wait();    // synchronously wait till the socket establishes connection
        }
    }
}
