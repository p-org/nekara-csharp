using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Nekara.Networking;

namespace Nekara.Client
{
    /// <summary>
    /// This is a global singleton that gets accessed/updated dynamically during runtime.
    /// This is the container object for the Nekara client-side API, available at the <see cref="Client"/> field.
    /// The reason we expose this testing API as a global singleton is to decouple the testing service
    /// configuration parameters (e.g, IP address of the testing service, connection session ID, etc.) from the test code.
    /// </summary>
    /// 
    /// <remarks>
    /// We store the <see cref="SessionKey"/> as an <see cref="AsyncLocal{T}"/> for 2 main reasons:
    /// 1. When the API calls are made via the network, subsequent test runs can interfere with each other
    ///    due to the asynchronous nature of the network. For instance, a test run could proceed to the next
    ///    session, but receive a delayed response message that belongs to the previous session.
    ///    We avoid this kind of issues entirely by using a <see cref="AsyncLocal{T}"/> object that
    ///    dynamically references values depending on the call context.
    /// 2. Using the <see cref="AsyncLocal{T}"/> object, multiple test sessions can run in parallel
    ///    (as long as the test itself does not depend on static variables). This allows for speedy testing.
    /// </remarks>
    public static class RuntimeEnvironment
    {
        public static bool DebugMode = false;
        public static int PrintVerbosity = 0;
        public static NekaraClient Client { get; set; }

        public static DateTime StartedAt = DateTime.Now;
        
        public static AsyncLocal<(string, int)> SessionKey = new AsyncLocal<(string, int)>();
        // public static AsyncLocal<int> RunNumber = new AsyncLocal<int>();
        public static ConcurrentDictionary<string, int> SessionCounter = new ConcurrentDictionary<string, int>();

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
            socket.config.PrintVerbosity = PrintVerbosity;

            // testing service proxy object;uses the socket to communicate to the actual testing service
            Client = new NekaraClient(socket);

            socket.ReadyFlag.Wait();    // synchronously wait till the socket establishes connection

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((sender, args) =>
            {
                Exception ex = (Exception)args.ExceptionObject;
                Console.WriteLine("\n<<< {0} Was Not Handled >>>", ex.GetType().Name);
                // PrintExceptionVerbose(ex);
            });
        }

        public static void SetCurrentSession(string sessionId)
        {
            if (!SessionCounter.ContainsKey(sessionId)) SessionCounter.TryAdd(sessionId, 0);
            SessionKey.Value = (sessionId, SessionCounter[sessionId]++);
        }

        private static void PrintExceptionVerbose(Exception exception, string indent = "||")
        {
            Console.WriteLine(indent + exception.GetType().Name + ": " + exception.Message);
            Console.WriteLine(indent + exception.StackTrace);
            if (exception is AggregateException)
            {
                ((AggregateException)exception).Handle(ex =>
                {
                    PrintExceptionVerbose(ex, indent + "||||");
                    return true;
                });
            }
            else if (exception.InnerException != null) PrintExceptionVerbose(exception.InnerException, indent + "||||");
        }
    }
}
