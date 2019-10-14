using System;
using System.Reflection;
using AsyncTester;
using AsyncTester.Networking;
using AsyncTester.Client;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Provide Benchmark Name and Iteration Count");
                Console.WriteLine("e.g. Benchmarks.exe DiningPhilosophers5.Run 100");
                return;
            }

            var info = args[0].Split('.');
            var typeName = info[0];
            var methodName = info[1];
            var repeat = Int32.Parse(args[1]);

            Console.WriteLine("Running Benchmarks...");

            // client-side socket
            OmniClient socket = new OmniClient(new OmniClientConfiguration());

            // testing service proxy object;uses the socket to communicate to the actual testing service
            TestingServiceProxy client = new TestingServiceProxy(socket);

            socket.ReadyFlag.Wait();    // synchronously wait till the socket establishes connection

            client.LoadTestSubject(Assembly.GetExecutingAssembly());

            var testMethod = client.GetMethodToBeTested(typeName, methodName);

            for (int i = 0; i < repeat; i++)
            {
                var run = client.RunTest(testMethod, Helpers.RandomInt()).Task;
                run.Wait();
            }

            socket.Dispose();
        }
    }
}
