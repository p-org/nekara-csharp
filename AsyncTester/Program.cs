using System;
using AsyncTester.Core;

namespace AsyncTester
{
    // NuGet Dependencies:
    //  Newtonsoft.Json  (JSON.NET)
    //  grpc
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Concurrency Tester...");

            // Initialize a tester server
            TestingService service = new TestingService();
            OmniServer socket = new OmniServer(new OmniServerConfiguration());
            socket.RegisterService(service);

            Console.ReadLine(); // TesterServer is an asynchronous object so we block the thread to prevent the program from exiting.

            Console.WriteLine("... Bye");
        }
    }
}
