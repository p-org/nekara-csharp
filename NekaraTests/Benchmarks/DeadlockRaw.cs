using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class Deadlock
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        static int x = 0;
        static bool lck = false;

        [TestMethod]
        public static void Run()
        {
            // initialize all relevant state
            lck = false;
            x = 0;

            nekara.CreateTask();
            Task.Run(() => Foo());

            nekara.CreateTask();
            Task.Run(() => Bar());

            Task.Run(() => Distraction());
        }

        static void Foo()
        {
            nekara.StartTask(1);
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
            nekara.EndTask(1);
        }

        static void Bar()
        {
            nekara.StartTask(2);
            //Acquire();

            nekara.ContextSwitch();
            x = 1;

            // Release();

            Console.WriteLine("Bar EndTask");
            nekara.EndTask(2);
        }

        static void Distraction()
        {
            // nekara.StartTask(2);

            nekara.ContextSwitch();
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
