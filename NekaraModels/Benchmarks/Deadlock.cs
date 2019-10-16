using System;
using Nekara.Core;
using Nekara.Client;
using Nekara.Models;

namespace Benchmarks
{
    class Deadlock
    {
        static int x = 0;
        static ITestingService nekara = RuntimeEnvironment.Client.Api;
        static Lock lck;

        [TestMethod]
        public async static void Execute()
        {
            // initialize all relevant state
            lck = new Lock(0);
            x = 0;

            //nekara.Api.CreateTask();
            var t1 = Task.Run(() => Foo());

            //nekara.Api.CreateTask();
            var t2 = Task.Run(() => Bar());

            await t1;
            await t2;
        }

        static void Foo()
        {
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
        }

        static void Bar()
        {
            //lck.Acquire();

            nekara.ContextSwitch();
            x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
        }
    }
}