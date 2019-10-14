using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AsyncTester.Client;

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

        static TestingServiceProxy ts;

        [TestMethod]
        static void Execute(TestingServiceProxy ts)
        {
            // initialize all relevant state
            Program.ts = ts;
            x = 0;
            lck = false;

            ts.Api.CreateTask();
            Task.Run(() => Foo());

            ts.Api.CreateTask();
            Task.Run(() => Bar());
        }

        static void Foo()
        {
            ts.Api.StartTask(1);

            Console.WriteLine("Foo/Acquire()");
            Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            ts.Api.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            ts.Api.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            Release();

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

        static void Acquire()
        {
            Console.WriteLine("Acquire()");
            ts.Api.ContextSwitch();
            while(true)
            {
                if(lck == false)
                {
                    lck = true;
                    break;
                }
                else
                {
                    ts.Api.BlockedOnResource(0);
                    continue;
                }
            }
        }

        static void Release()
        {
            Console.WriteLine("Release()");
            ts.Api.Assert(lck == true, "Release called on non-acquired lock");

            lck = false;
            ts.Api.SignalUpdatedResource(0);
        }
    }
}
