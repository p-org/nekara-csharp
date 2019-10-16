using System;
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;

namespace Benchmarks
{
    class Deadlock
    {
        static int x = 0;
        static bool lck = false;

        static ITestingService nekara;

        [TestMethod]
        public static void Execute()
        {
            // initialize all relevant state
            Deadlock.nekara = RuntimeEnvironment.Client.Api;
            x = 0;
            lck = false;

            Task.Run(() => Foo());

            Task.Run(() => Bar());
        }

        static void Foo()
        {
            Console.WriteLine("Foo/Acquire()");
            Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            Release();

            nekara.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo EndTask");
        }

        static void Bar()
        {
            //Acquire();

            nekara.ContextSwitch();
            x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
        }

        static void Acquire()
        {
            Console.WriteLine("Acquire()");
            nekara.ContextSwitch();
            while (true)
            {
                if (lck == false)
                {
                    lck = true;
                    break;
                }
                else
                {
                    nekara.BlockedOnResource(0);
                    continue;
                }
            }
        }

        static void Release()
        {
            Console.WriteLine("Release()");
            nekara.Assert(lck == true, "Release called on non-acquired lock");

            lck = false;
            nekara.SignalUpdatedResource(0);
        }
    }
}
