using System;
using Xunit;
using NekaraManaged.Client;
using System.Threading.Tasks;
// using Nekara.Models;

namespace NekaraUnitTest
{
    public class DeadlockRaw
    {
        public static NekaraManagedClient nekara = RuntimeEnvironment.Client;

        static int x = 0;
        static bool lck = false;
        // static bool bugFound = false;

        [Fact(Timeout = 5000)]
        public void RunBasic()
        {
            nekara.Api.CreateSession();

            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            Task.Run(() => Foo());

            nekara.Api.CreateTask();
            Task.Run(() => Bar());

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public void RunBlocking()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.Api.CreateTask();
            var t2 = Task.Run(() => Bar());

            nekara.Api.CreateTask();
            var all = Task.Run(() => {
                nekara.Api.StartTask(4);
                Task.WhenAll(t1, t2).Wait();
                nekara.Api.EndTask(4);
            });

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public Task RunBasicTask()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.Api.CreateTask();
            var t2 = Task.Run(() => Bar());

            nekara.Api.WaitForMainTask();

            return Task.WhenAll(t1, t2);
        }

        [Fact(Timeout = 5000)]
        public Task RunBlockingTask()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.Api.CreateTask();
            var t2 = Task.Run(() => Bar());

            nekara.Api.WaitForMainTask();

            Task.WhenAll(t1, t2).Wait();

            return Task.CompletedTask;
        }

        [Fact(Timeout = 5000)]
        public async Task RunBlockingAsync()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.Api.CreateTask();
            var t2 = Task.Run(() => Bar());

            nekara.Api.WaitForMainTask();

            await Task.WhenAll(t1, t2);

            return;
        }

        [Fact(Timeout = 5000)]
        public void RunLiveLock()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            Task.Run(() => Foo());

            nekara.Api.CreateTask();
            Task.Run(() => Bar());

            Task.Run(() => Distraction());  // this is an undeclared Task, so we should expect the server to fail.
        }

        /* [TestMethod]
        public static void RunLiveLockTrivial()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            Task.Run(() => {
                nekara.Api.StartTask(1);

                nekara.Api.ContextSwitch();

                nekara.Api.EndTask(1);
            });

            Task.Run(() =>
            {
                nekara.Api.ContextSwitch();
            });
        }

        [TestMethod]
        public static void RunUserMistake()
        {
            // initialize all relevant state
            nekara.Api.CreateResource(0);
            lck = false;
            x = 0;

            nekara.Api.CreateTask();
            Task.Run(() => Foo());

            nekara.Api.CreateTask();
            Task.Run(() => Bar());

            Task.Run(() => UndeclaredTask());  // this is an undeclared Task, so we should expect the server to fail.
        } */

        internal void Foo()
        {
            nekara.Api.StartTask(1);
            Console.WriteLine("Foo/Acquire()");
            Acquire();

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.Api.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo/ContextSwitch()");
            nekara.Api.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo/Release()");
            Release();

            // nekara.Assert(lx1 == lx2, "Race!");
            /* if (!(lx1 == lx2))
            {
                bugFound = true;
            } */

            Console.WriteLine("Foo EndTask");
            nekara.Api.EndTask(1);
        }

        internal void Bar()
        {
            nekara.Api.StartTask(2);
            Acquire();

            nekara.Api.ContextSwitch();
            x = 1;

            Release();

            Console.WriteLine("Bar EndTask");
            nekara.Api.EndTask(2);
        }

        internal void Distraction()
        {
            // nekara.Api.StartTask(3);

            nekara.Api.ContextSwitch();
        }

        internal void UndeclaredTask()
        {
            nekara.Api.StartTask(3);

            nekara.Api.ContextSwitch();
        }

        internal void Acquire()
        {
            Console.WriteLine("Acquire()");
            nekara.Api.ContextSwitch();
            while (true)
            {
                if (lck == false)
                {
                    lck = true;
                    break;
                }
                else
                {
                    nekara.Api.BlockedOnResource(0);
                    continue;
                }
            }
        }

        internal void Release()
        {
            Console.WriteLine("Release()");
            nekara.Api.Assert(lck == true, "Release called on non-acquired lock");

            lck = false;
            nekara.Api.SignalUpdatedResource(0);
        }
    }
}
