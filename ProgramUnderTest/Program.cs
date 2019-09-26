using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace ProgramUnderTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }

        static int x = 0;
        static bool lck = false;

        static ITestingService testingService;

        [TestMethod]
        static void Execute(ITestingService testingService)
        {
            // initialize all relevant state
            Program.testingService = testingService;
            x = 0;
            lck = false;

            testingService.CreateTask();
            Task.Run(() => Foo());

            testingService.CreateTask();
            Task.Run(() => Bar());
        }

        static void Foo()
        {
            testingService.StartTask(1);

            Acquire();

            testingService.ContextSwitch();
            int lx1 = x;

            testingService.ContextSwitch();
            int lx2 = x;

            Release();

            testingService.Assert(lx1 == lx2, "Race!");

            testingService.EndTask(1);
        }

        static void Bar()
        {
            testingService.StartTask(2);

            //Acquire();

            testingService.ContextSwitch();
            x = 1;

            //Release();

            testingService.EndTask(2);
        }

        static void Acquire()
        {
            testingService.ContextSwitch();
            while(true)
            {
                if(lck == false)
                {
                    lck = true;
                    break;
                }
                else
                {
                    testingService.BlockedOnResource(0);
                    continue;
                }
            }
        }

        static void Release()
        {
            testingService.Assert(lck == true, "Release called on non-acquired lock");

            lck = false;
            testingService.SignalUpdatedResource(0);
        }
    }
}
