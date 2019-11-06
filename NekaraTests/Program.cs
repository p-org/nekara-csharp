using System;
using System.IO;
using System.Linq;
using System.Reflection;
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

                var run = client.RunTest(testDefinition, repeat).Task;
                run.Wait();
            }

            client.PrintTestResults();

            client.Dispose();
        }
    }
}
