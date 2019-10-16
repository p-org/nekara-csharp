using System;
using Nekara.Core;
using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    class Deadlock
    {
        static int x = 0;
        static ITestingService ts;
        static Lock lck;

        [TestMethod]
        public async static void Execute()
        {
            // initialize all relevant state
            Deadlock.ts = RuntimeEnvironment.Client.Api;

            lck = new Lock(0);
            x = 0;

            //ts.Api.CreateTask();
            var t1 = Task.Run(() => Foo());

            //ts.Api.CreateTask();
            var t2 = Task.Run(() => Bar());

            await t1;
            await t2;
        }

        static void Foo()
        {
            Console.WriteLine("Foo/Acquire()");
            lck.Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            ts.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            ts.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            lck.Release();

            ts.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo EndTask");
        }

        static void Bar()
        {
            lck.Acquire();

            ts.ContextSwitch();
            x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
        }
    }
}