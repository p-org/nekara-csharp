using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AsyncTester;
using AsyncTester.Core;
using System.Threading;
using System.Dynamic;

namespace ClientProgram
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Test Client...");

            // client-side socket
            OmniClient socket = new OmniClient(new OmniClientConfiguration());

            // testing service proxy object;uses the socket to communicate to the actual testing service
            TestingServiceProxy client = new TestingServiceProxy(socket);



            // using the service interactively in this program
            Repl(client);

            Console.WriteLine("... Bye");
        }

        static void Repl(TestingServiceProxy client)
        {
            var cancellation = new CancellationTokenSource();
            Helpers.AsyncTaskLoop(() =>
            {
                Console.Write("\n\n\nPath of the Program To Test? ");
                string input = Console.ReadLine();
                input = Regex.Replace(input, @"[ \t]+", " ");

                // Load assembly and notify the server - this is asynchronous
                client.LoadTestSubject(input);
                var testMethod = client.GetMethodToBeTested();
                Console.WriteLine("... found method to be tested: [{0}]", testMethod.Name);

                // Ask how many iterations
                int repeat = Helpers.PromptInt("How many iterations? ", 0, 500);
                if (repeat > 0) return Helpers.RepeatTask(() => client.RunTest(testMethod).task, repeat);
                else cancellation.Cancel();

                return Task.CompletedTask;
            }, cancellation.Token);

            // block the main thread here to prevent exiting - as AsyncTaskLoop will return immediately
            while (true)
            {
                if (cancellation.IsCancellationRequested) break;
                Thread.Sleep(1000);
            }
        }
    }
}
