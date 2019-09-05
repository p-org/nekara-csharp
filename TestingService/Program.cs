using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.PSharp;
using Microsoft.PSharp.TestingServices;

namespace TestingService
{
    class Program
    {
        static Assembly assembly;

        public static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("Usage: TestingService program.dll");
                return;
            }

            if(args.Any(s => s == "/break"))
            {
                System.Diagnostics.Debugger.Launch();
            }

            // Load assembly
            try
            {
                assembly = Assembly.LoadFrom(args[0]);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
                return;
            }

            // find test method
            List<MethodInfo> testMethods = null;
            var bindingFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.InvokeMethod;

            try
            {
                testMethods = assembly.GetTypes().SelectMany(t => t.GetMethods(bindingFlags))
                    .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), false).Length > 0).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var le in ex.LoaderExceptions)
                {
                    Console.WriteLine("{0}", le.Message);
                }

                Console.WriteLine($"Failed to load assembly '{assembly.FullName}'");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex.Message);
                Console.WriteLine($"Failed to load assembly '{assembly.FullName}'");
                return;
            }

            if(testMethods.Count == 0)
            {
                Console.WriteLine("Did not find any test method");
                return;
            }

            if(testMethods.Count > 1)
            {
                Console.WriteLine("Found multiple test methods");
                foreach(var tm in testMethods)
                {
                    Console.WriteLine("Method: {0}", tm.Name);
                }
                Console.WriteLine("Only one test method supported");
                return;
            }

            var testMethod = testMethods[0];

            if(testMethod.GetParameters().Length != 1 ||
                testMethod.GetParameters()[0].ParameterType != typeof(ITestingService))
            {
                Console.WriteLine("Incorrect signature of the test method");
                return;
            }

            RunTester(testMethod);
        }

        static void RunTester(MethodInfo testMethod)
        {
            // -i:100 -max-steps:100
            var configuration = Configuration.Create()
                .WithNumberOfIterations(100)
                .WithMaxSteps(100)
                .WithVerbosityEnabled();
            
            var engine = TestingEngineFactory.CreateBugFindingEngine(configuration, r =>
            {
                r.SetLogger(new Microsoft.PSharp.IO.ConsoleLogger());
                r.CreateMachine(typeof(TopLevelMachine), new TopLevelMachineInitEvent { testMethod = testMethod });
            });
            engine.Run();

            Console.WriteLine("Errors found: {0}", engine.TestReport.NumOfFoundBugs);
            foreach(var bugreport in engine.TestReport.BugReports)
            {
                Console.WriteLine("{0}", bugreport);
            }
        }

    }
}
