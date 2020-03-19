using System;
using Xunit;
using NekaraManaged.Client;
using System.Threading.Tasks;
// using Nekara.Models;

namespace NekaraUnitTest
{
    public class Deadlock
    {
        static int x = 0;
        static Nekara.Models.Lock lck;
        static bool bugfound = false;

        [Fact(Timeout = 5000)]
        public static void Racebetween2Tasks()
        {

            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                // initialize all relevant state
                lck = new Nekara.Models.Lock(0);
                x = 0;

                nekara.Api.CreateTask();
                var T1 = Task.Run(() => Foo());

                nekara.Api.CreateTask();
                var T2 = Task.Run(() => Bar());

                
                nekara.Api.WaitForMainTask();
                Task.WaitAll(T1, T2);
            } 
        }

        static void Foo()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.StartTask(1);

            Console.WriteLine("Foo/Acquire()");
            lck.Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.Api.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.Api.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            lck.Release();

            // nekara.Api.Assert(lx1 == lx2, "Race!");
            if (!(lx1 == lx2))
            {
                bugfound = true;
            }

            Console.WriteLine("Foo EndTask");
            nekara.Api.EndTask(1);
        }

        static void Bar()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.StartTask(2);
            //lck.Acquire();

            nekara.Api.ContextSwitch();
            x = 1;

            //lck.Release();

            Console.WriteLine("Bar EndTask");
            nekara.Api.EndTask(2);
        }
    }
}
