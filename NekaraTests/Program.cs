using System;
using System.Reflection;
using Nekara.Client;

namespace NekaraTests
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

            var random = new Random(DateTime.Now.Second);

            Console.WriteLine("Running Benchmarks...");

            NekaraClient client = RuntimeEnvironment.Client;

            var assembly = Assembly.GetExecutingAssembly();
            var testMethod = client.GetMethodToBeTested(assembly, typeName, methodName);

            for (int i = 0; i < repeat; i++)
            {
                var run = client.RunTest(testMethod, random.Next()).Task;
                run.Wait();
            }

            client.Dispose();
        }
    }
}
