using System;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace Benchmarks
{
    class Deadlock2
    {
        static int x = 0;
        static IAsyncLock lck;

        static ITestingService testingService;

        [TestMethod]
        public static void Execute(ITestingService testingService)
        {
            // initialize all relevant state
            Deadlock2.testingService = testingService;

            lck = Deadlock2.testingService.CreateLock(0);
            x = 0;

            testingService.CreateTask();
            Task.Run(() => Foo());

            testingService.CreateTask();
            Task.Run(() => Bar());
        }

        static void Foo()
        {
            testingService.StartTask(1);

            Console.WriteLine("Foo/Acquire()");
            lck.Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            testingService.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            testingService.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            lck.Release();

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
    }
}