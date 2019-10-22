using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Nekara.Networking;
using System.Threading;
using System.Reflection;

namespace Nekara.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting Test Client...");

            NekaraClient client = RuntimeEnvironment.Client;

            // if command line args given, proceed accordingly
            // the argument format is:
            // e.g.> ClientProgram.exe run Benchmarks/bin/Debug/Benchmarks.dll 0 50
            // where the 0 indicates the index of the test method found in Benchmarks.dll
            // and 50 indicates the number of iterations
            if (args.Length > 0)
            {
                var command = args[0];

                if (command == "run")
                {
                    if (args.Length < 4) throw new Exception("Need to provide all the arguments");

                    var path = args[1];
                    var choice = Int32.Parse(args[2]);
                    var repeat = Int32.Parse(args[3]);

                    var assembly = Assembly.LoadFrom(path);

                    var methods = client.ListTestMethods(assembly);
                    var testMethod = methods[choice];
                    var testDefinition = client.GetTestDefinition(testMethod);

                    var run = client.RunTest(testDefinition, repeat).Task;
                    run.Wait();

                    client.PrintTestResults();

                    client.socket.Dispose();

                }
                else if (command == "replay")
                {
                    string sessionId = args[1];

                    // Make a replay request
                    var run = client.ReplayTestSession(sessionId).Task;

                    run.Wait();

                    client.socket.Dispose();
                }
                else
                {
                    Console.WriteLine("Unknown command '" + command + "'");
                }
            }
            else
            {
                // if no args are given, run in interactive mode
                Repl(client);
            }

            Console.WriteLine("... Bye");
        }

        static void Repl(NekaraClient client)
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
                var assembly = Assembly.LoadFrom(path);
                var methods = client.ListTestMethods(assembly);
                Console.WriteLine(String.Join("\n", methods.Select((info, index) => "    " + index.ToString() + ") " + info.DeclaringType.FullName + "." + info.Name)));

                var choice = Helpers.PromptInt("Which method? ", 0, methods.Count - 1);

                var testMethod = methods[choice];
                Console.WriteLine("... selected method: [{0}]", testMethod.DeclaringType.Name + "." + testMethod.Name);
                
                var testDefinition = client.GetTestDefinition(testMethod);

                // Ask how many iterations
                int repeat = Helpers.PromptInt("How many iterations? ", 0, 500);
                
                if (repeat > 0)
                {
                    var run = client.RunTest(testDefinition, repeat).Task;
                    run.Wait();
                    client.PrintTestResults();
                }
                return Task.CompletedTask;
            });
            // replay a test run
            actions.Add("replay", () =>
            {
                string sessionId = Helpers.Prompt("Provide Test Session ID? ", input => true);

                // Make a replay request
                return client.ReplayTestSession(sessionId).Task;
            });

            Helpers.AsyncTaskLoop(() =>
            {
                Console.Write("Commands:\n    run: run concurrency test\n    replay: replay a test run\n    exit: exit program\n\n");

                string choice = Helpers.Prompt("Enter a command? ", input => actions.ContainsKey(input));

                return actions[choice]().ContinueWith(prev => Task.Delay(500)); // delaying a little bit to wait for pending console IO

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
