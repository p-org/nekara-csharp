using System;
using Nekara.Client;
using Nekara.Core;
using Nekara.Models;

namespace Nekara.Tests.Benchmarks
{
    class Deadlock2
    {
        static int x = 0;
        static ITestingService nekara = RuntimeEnvironment.Client.Api;
        static Lock lck;

        [TestMethod]
        public static void Execute()
        {
            // initialize all relevant state
            lck = new Lock(0);
            x = 0;

            Task.Run(() => Foo());

            Task.Run(() => Bar());
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

            //lck.Release();

            Console.WriteLine("Bar EndTask");
        }
    }
}