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
using System.Threading;
using System.Dynamic;

namespace ClientProgram
{
    class Program
    {
        static Assembly assembly;
        static int SchedulingSeed = 0;

        static TesterClient client;

        static void Main(string[] args)
        {
            Console.WriteLine("Starting Test Client...");

            // Initialize a tester client (this should actually be done in a different process)
            TesterClient client = new TesterClient(new TesterConfiguration());

            Repl(client);

            Console.WriteLine("... Bye");
        }

        static void Repl(TesterClient client)
        {
            var cancellation = new CancellationTokenSource();
            Helpers.AsyncTaskLoop(() =>
            {
                Console.Write("Path of the Program To Test: ");
                string input = Console.ReadLine();
                input = Regex.Replace(input, @"[ \t]+", " ");

                // Load assembly
                try
                {
                    client.service.SetAssembly(input);
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }

                try
                {
                    var testMethod = client.service.GetMethodToBeTested();
                    Console.WriteLine("... found method to be tested: [{0}]", testMethod.Name);

                    Console.Write("Start test (y/n)? ");
                    input = Console.ReadLine();

                    if (input == "y") return client.service.RunTest(testMethod);
                    else if (input == "n")
                    {
                        cancellation.Cancel();
                        return Task.CompletedTask;
                    }
                    else return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}", ex.Message);
                    Console.WriteLine($"Failed to load assembly '{assembly.FullName}'");
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }
            }, cancellation.Token);

            // block the main thread here to prevent exiting - as AsyncTaskLoop will return immediately
            while (true)
            {
                if (cancellation.IsCancellationRequested) break;
            }
        }
    }
}
