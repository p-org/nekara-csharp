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
                Console.Write("Path of the Program To Test: ");
                string input = Console.ReadLine();
                input = Regex.Replace(input, @"[ \t]+", " ");

                // Load assembly and notify the server - this is asynchronous
                var routine = client.LoadTestSubject(input)
                .Then((object sessionId) =>
                {
                    var testMethod = client.GetMethodToBeTested();
                    Console.WriteLine("... found method to be tested: [{0}]", testMethod.Name);
                    Console.Write("Start test (y/n)? ");
                    input = Console.ReadLine();

                    if (input == "y") return client.RunTest(testMethod);
                    else if (input == "n") cancellation.Cancel();
                    return null;

                }).Catch(error =>
                {
                    Console.WriteLine("Error: {0}", error);
                    cancellation.Cancel();
                    return null;
                });

                return routine.task;
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
