using System;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks
{
    class Deadlock2
    {
        static int x = 0;
        static TestingServiceProxy ts;
        static IAsyncLock lck;

        [TestMethod]
        public static void Execute(TestingServiceProxy ts)
        {
            // initialize all relevant state
            Deadlock2.ts = ts;

            lck = ts.LockFactory.CreateLock(0);
            x = 0;

            ts.Api.CreateTask();
            Task.Run(() => Foo());

            ts.Api.CreateTask();
            Task.Run(() => Bar());
        }

        static void Foo()
        {
            ts.Api.StartTask(1);

            Console.WriteLine("Foo/Acquire()");
            lck.Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            ts.Api.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            ts.Api.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            lck.Release();

            ts.Api.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo EndTask");
            ts.Api.EndTask(1);
        }

        static void Bar()
        {
            ts.Api.StartTask(2);

            //Acquire();

            ts.Api.ContextSwitch();
            x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
            ts.Api.EndTask(2);
        }
    }
}