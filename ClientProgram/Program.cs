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
using System.Net.Sockets;

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
            var actions = new Dictionary<string, Func<Task>>();  // user command handlers

            // exit command
            actions.Add("exit", () =>
            {
                client.socket.Dispose();
                cancellation.Cancel();
                return Task.CompletedTask;
            });
            // run test
            actions.Add("run", () =>
            {
                string path = Helpers.Prompt("Path of the Program To Test? ", input => File.Exists(input));

                // Load assembly and notify the server - this is asynchronous
                client.LoadTestSubject(path);

                var methods = client.ListTestMethods();
                Console.WriteLine(String.Join("\n", methods.Select((info, index) => "    " + index.ToString() + ") " + info.DeclaringType.Name + "." + info.Name)));

                var choice = Helpers.PromptInt("Which method? ", 0, methods.Count - 1);

                var testMethod = methods[choice];
                Console.WriteLine("... selected method: [{0}]", testMethod.DeclaringType.Name + "." + testMethod.Name);

                // Ask how many iterations
                int repeat = Helpers.PromptInt("How many iterations? ", 0, 500);
                if (repeat > 0) return Helpers.RepeatTask(() => client.RunTest(testMethod).task, repeat);
                else return Task.CompletedTask;
            });
            // replay a test run
            actions.Add("replay", () =>
            {
                string sessionId = Helpers.Prompt("Provide Test Session ID? ", input => true);

                // Make a replay request
                return client.ReplayTestSession(sessionId);
            });

            Helpers.AsyncTaskLoop(() =>
            {
                Console.Write("Commands:\n    run: run concurrency test\n    replay: replay a test run\n    exit: exit program\n\n");

                string choice = Helpers.Prompt("Enter a command? ", input => actions.ContainsKey(input));

                return actions[choice]().ContinueWith(prev => Task.Delay(1000)); // delaying a little bit to wait for pending console IO

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
