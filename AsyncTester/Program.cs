using System;

namespace AsyncTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Concurrency Tester...");

            // Initialize a tester server
            TesterServer server = new TesterServer(new TesterConfiguration());

            Console.ReadLine();

            // Initialize a tester client (this should actually be done in a different process)
            // TesterClient client = new TesterClient(new TesterConfiguration());

            Console.WriteLine("... Bye");
        }
    }
}
