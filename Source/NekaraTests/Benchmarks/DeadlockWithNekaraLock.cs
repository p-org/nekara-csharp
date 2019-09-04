using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class DeadlockWithNekaraLock
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        static int x = 0;
        static Nekara.Models.Lock lck;

        [TestMethod]
        public static void Run()
        {
            // initialize all relevant state
            lck = new Nekara.Models.Lock(0);
            x = 0;

            nekara.CreateTask();
            Task.Run(() => Foo());

            nekara.CreateTask();
            Task.Run(() => Bar());
        }

        static void Foo()
        {
            nekara.StartTask(1);

            Console.WriteLine("Foo/Acquire()");
            lck.Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            lck.Release();

            nekara.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo EndTask");
            nekara.EndTask(1);
        }

        static void Bar()
        {
            nekara.StartTask(2);
            //lck.Acquire();

            nekara.ContextSwitch();
            x = 1;

            //lck.Release();

            Console.WriteLine("Bar EndTask");
            nekara.EndTask(2);
        }
    }
}
