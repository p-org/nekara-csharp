using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Nekara;
using Nekara.Client;
using Nekara.Models;

namespace NekaraTests
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                if (args.Length == 1)
                {
                    var compiler = new NekaraCompiler();
                    string source = File.ReadAllText(args[0]);

                    var userCode = compiler.Compile(source);
                    var type = userCode.GetType("Nekara.Models.Benchmarks.CompileTest");
                    var method = type.GetMethod("Hello");
                    // method.Invoke(null, null);

                    Console.WriteLine(userCode);
                    return;
                }

                Console.WriteLine("Provide Benchmark Name and Iteration Count");
                Console.WriteLine("e.g. Benchmarks.exe DiningPhilosophers5.Run 100");
                return;
            }

            // var random = new Random(DateTime.Now.Second);

            Console.WriteLine("Running Benchmarks...");

            NekaraClient client = RuntimeEnvironment.Client;

            if (args[0] == "replay")
            {
                // Make a replay request
                var run = client.ReplayTestSession(args[1]).Task;
                run.Wait();
            }
            else
            {
                var info = args[0].Split('.');
                var typeName = String.Join(".", info.Take(info.Length - 1));
                var methodName = info.Last();
                var repeat = Int32.Parse(args[1]);

                var assembly = Assembly.GetExecutingAssembly();
                var testMethod = client.GetMethodToBeTested(assembly, typeName, methodName);
                var testDefinition = client.GetTestDefinition(testMethod);

                /*for (int i = 0; i < repeat; i++)
                {
                    var run = client.RunTest(testMethod, random.Next()).Task;
                    run.Wait();
                }*/

                var summaryFile = File.AppendText("logs/benchmark-summary-" + DateTime.Now.Ticks.ToString() + ".csv");
                summaryFile.WriteLine("Test,NumSchedules,MinSteps,AvgSteps,MaxSteps,OverSteps,ElapsedClient,ElapsedServer");

                var multipleRuns = Helpers.RepeatTask(() => new Promise((resolve, reject) =>
                {
                    var beginAt = Stopwatch.GetTimestamp();

                    var run = client.RunTest(testDefinition, repeat, terminateOnFirstFail: true).Task;
                    run.Wait();

                    var elapsed = (Stopwatch.GetTimestamp() - beginAt) / 10000;

                    var summary = (TestSummary)run.Result;
                    summary.elapsedClient = elapsed;

                    Console.WriteLine("... Elapsed {0} sec", elapsed / 1000);
                    Console.WriteLine(summary.ToString());

                    summaryFile.WriteLine($"{typeName},{summary.iterations},{summary.minDecisions},{summary.avgDecisions},{summary.maxDecisions},{summary.maxDecisionsReached},{summary.elapsedClient},{summary.elapsedServer}");
                    summaryFile.Flush();

                    resolve(null);
                }).Task, 101);

                multipleRuns.Wait();
            }

            client.PrintTestResults();

            client.Dispose();
        }
    }
}
