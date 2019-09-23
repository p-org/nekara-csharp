using System;

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
            TesterServer server = new TesterServer(new TesterConfiguration());

            Console.ReadLine(); // TesterServer is an asynchronous object so we block the thread to prevent the program from exiting.

            // Initialize a tester client (this should actually be done in a different process)
            // TesterClient client = new TesterClient(new TesterConfiguration());

            Console.WriteLine("... Bye");
        }
    }
}
