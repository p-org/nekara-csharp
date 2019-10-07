using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    class Deadlock
    {
        static int x = 0;
        static bool lck = false;

        static ITestingService testingService;

        [TestMethod]
        public static void Execute(ITestingService testingService)
        {
            // initialize all relevant state
            Deadlock.testingService = testingService;
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

            Console.WriteLine("Foo/Acquire()");
            Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            testingService.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            testingService.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            Release();

            testingService.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo EndTask");
            testingService.EndTask(1);
        }

        static void Bar()
        {
            testingService.StartTask(2);

            //Acquire();

            testingService.ContextSwitch();
            x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
            testingService.EndTask(2);
        }

        static void Acquire()
        {
            Console.WriteLine("Acquire()");
            testingService.ContextSwitch();
            while (true)
            {
                if (lck == false)
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
            Console.WriteLine("Release()");
            testingService.Assert(lck == true, "Release called on non-acquired lock");

            lck = false;
            testingService.SignalUpdatedResource(0);
        }
    }
}
