using System;
using Xunit;
using NekaraManaged.Client;
using System.Threading.Tasks;
using System.Threading;
// using Nekara.Models;

namespace NekaraUnitTest
{
    public class Deadlock
    {
        static int x = 0;
        static Nekara.Models.Lock lck;
        static bool bugfound = false;

        [Fact(Timeout = 5000)]
        public void Racebetween2Tasks()
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

        [Fact(Timeout = 5000)]
        public void Deadlock3Tasksand1Lock()
        {
            DeadlockWithNekaraLock2 dlk = new DeadlockWithNekaraLock2();

            while (!dlk.bugFound)
            {
                dlk.nekara.Api.CreateTask();
                Task t1 = Task.Run(() => dlk.Foo(1));

                dlk.nekara.Api.CreateTask();
                Task t2 = Task.Run(() => dlk.Foo(2));

                dlk.nekara.Api.CreateTask();
                Task t3 = Task.Run(() => dlk.Foo(3));

                dlk.nekara.Api.WaitForMainTask();

                Task.WaitAll(t1, t2, t3);

                dlk.Reset();
            }

        }

        public class DeadlockWithNekaraLock2
        {
            private int x = 0;
            private Nekara.Models.Lock lck;
            public NekaraManagedClient nekara;
            public bool bugFound;
            private bool t2;
            private bool t3;

            public DeadlockWithNekaraLock2()
            {
                this.nekara = RuntimeEnvironment.Client;
                this.nekara.Api.CreateSession();

                this.x = 0;
                this.lck = new Nekara.Models.Lock(0);
                this.bugFound = false;
                this.t2 = false;
                this.t3 = false;
            }

            public void Reset()
            {
                this.nekara.Api.CreateSession();

                this.x = 0;
                this.lck = new Nekara.Models.Lock(0);
                this.t2 = false;
                this.t3 = false;
            }

            public void Foo(int taskId)
            {
                this.nekara.Api.StartTask(taskId);

                bool temp = !(this.t2 && this.t3) && ((this.t2 && !this.t3) || (!this.t2 && this.t3));
                if ((x == 1) && temp)
                {
                    this.bugFound = true;
                }

                if (this.bugFound == true)
                {
                    this.nekara.Api.EndTask(taskId);
                    return;
                }

                Console.WriteLine("Foo({0})/Acquire()", taskId);
                lck.Acquire();

                if (taskId == 2)
                {
                    this.t2 = true;
                }
                else if (taskId == 3)
                {
                    this.t3 = true;
                }

                x = taskId;
                Console.WriteLine("Foo({0})/ContextSwitch():0", taskId);
                this.nekara.Api.ContextSwitch();

                Console.WriteLine("Foo({0})/ContextSwitch():1", taskId);
                this.nekara.Api.ContextSwitch();
                int lx1 = x;

                Console.WriteLine("Foo({0})/ContextSwitch():2", taskId);
                this.nekara.Api.ContextSwitch();
                int lx2 = x;

                Console.WriteLine("Foo({0})/Release()", taskId);
                if (taskId != 1) // lck.Release();
                {
                    lck.Release();
                }
                else
                {
                    if (!this.t2 || !this.t3)
                    {
                        // Deadlock
                        this.bugFound = true;
                    }
                }

                // nekara.Assert(lx1 == lx2, "Race!"); - Race will never happen
                Console.WriteLine("Foo({0})/EndTask()", taskId);
                this.nekara.Api.EndTask(taskId);
            }
        }


        [Fact(Timeout = 5000)]
        public void Racebetween2NekaraTasks()
        {
            DeadlockWithNekaraLockAndTask dlk = new DeadlockWithNekaraLockAndTask();

            while (!dlk.bugFound)
            {
                Nekara.Models.Task t1 = Nekara.Models.Task.Run(() => dlk.Foo());
                Nekara.Models.Task t2 = Nekara.Models.Task.Run(() => dlk.Bar());

                Nekara.Models.Task.WaitAll(t1, t2);

                dlk.nekara.Api.WaitForMainTask();
                dlk.Reset();
            }
        }

        public class DeadlockWithNekaraLockAndTask
        {
            public NekaraManagedClient nekara;
            public bool bugFound;
            private Nekara.Models.Lock lck;
            private int x;

            public DeadlockWithNekaraLockAndTask()
            {
                this.nekara = RuntimeEnvironment.Client;
                this.nekara.Api.CreateSession();
                this.lck = new Nekara.Models.Lock(0);
                this.bugFound = false;
                this.x = 0;
            }

            public void Reset()
            {
                this.nekara.Api.CreateSession();
                this.lck = new Nekara.Models.Lock(0);
                this.x = 0;
            }

            public void Foo()
            {
                Console.WriteLine("Foo/Acquire()");
                this.lck.Acquire();

                Console.WriteLine("Foo/ContextSwitch()");
                this.nekara.Api.ContextSwitch();
                int lx1 = this.x;

                Console.WriteLine("Foo/ContextSwitch()");
                this.nekara.Api.ContextSwitch();
                int lx2 = this.x;

                Console.WriteLine("Foo/Release()");
                this.lck.Release();

                // nekara.Assert(lx1 == lx2, "Race!");
                if (!(lx1 == lx2))
                {
                    this.bugFound = true;
                }

                Console.WriteLine("Foo EndTask");
            }

            public void Bar()
            {
                //lck.Acquire();

                this.nekara.Api.ContextSwitch();
                this.x = 1;

                //lck.Release();

                Console.WriteLine("Bar EndTask");
            }
        }

        [Fact(Timeout = 5000)]
        public void Racebetween2NekaraTasksAsync()
        {
            DeadlockWithNekaraLockAndAsync dlk = new DeadlockWithNekaraLockAndAsync();

            while (!dlk.bugFound)
            {
                var t1 = dlk.Foo();
                var t2 = dlk.Bar();

                Nekara.Models.Task.WaitAll(t1, t2);

                dlk.nekara.Api.WaitForMainTask();
                dlk.Reset();
            }
        }

        public class DeadlockWithNekaraLockAndAsync
        {
            public NekaraManagedClient nekara;
            public bool bugFound;
            private Nekara.Models.Lock lck;
            private int x;

            public DeadlockWithNekaraLockAndAsync()
            {
                this.nekara = RuntimeEnvironment.Client;
                this.nekara.Api.CreateSession();
                this.lck = new Nekara.Models.Lock(0);
                this.bugFound = false;
                this.x = 0;
            }

            public void Reset()
            {
                this.nekara.Api.CreateSession();
                this.lck = new Nekara.Models.Lock(0);
                this.x = 0;
            }

            internal async Nekara.Models.Task Foo()
            {
                Console.WriteLine("Foo/Acquire()");
                this.lck.Acquire();

                Console.WriteLine("Foo/ContextSwitch()");
                this.nekara.Api.ContextSwitch();
                int lx1 = this.x;

                Console.WriteLine("Foo/ContextSwitch()");
                this.nekara.Api.ContextSwitch();
                int lx2 = this.x;

                Console.WriteLine("Foo/Release()");
                this.lck.Release();

                // nekara.Assert(lx1 == lx2, "Race!");
                if (!(lx1 == lx2))
                {
                    this.bugFound = true;
                }

                Console.WriteLine("Foo EndTask");

                await Task.Delay(1);
            }

            internal async Nekara.Models.Task Bar()
            {
                //lck.Acquire();

                this.nekara.Api.ContextSwitch();
                this.x = 1;

                //lck.Release();

                Console.WriteLine("Bar EndTask");

                await Task.Delay(1);
            }
        }


        static bool lck1 = false;
        static bool lck2 = false;
        public static NekaraManagedClient nekara = RuntimeEnvironment.Client;
        static bool bugFoundDl = false;

        [Fact(Timeout = 5000)]
        public void Deadlock3()
        {

            nekara.Api.CreateSession();
            while (!bugFoundDl)
            {
                nekara.Api.CreateResource(0);
                nekara.Api.CreateResource(1);

                int counter = 1;
                Nekara.Models.Task t1 = Nekara.Models.Task.Run( () =>
                {
                    nekara.Api.ContextSwitch();
                    Acquire(true, 0);

                    nekara.Api.ContextSwitch();
                    Acquire(true, 1); // Deadlock

                    if (bugFoundDl)
                    {
                        return;
                    }

                    nekara.Api.ContextSwitch();
                    counter++;

                    nekara.Api.ContextSwitch();
                    Release(true, 0);

                    nekara.Api.ContextSwitch();
                    Release(true, 1);
                });

                Nekara.Models.Task t2 = Nekara.Models.Task.Run( () =>
                {
                    nekara.Api.ContextSwitch();
                    Acquire(false, 1);

                    nekara.Api.ContextSwitch();
                    Acquire(false, 0); // Deadlock

                    if (bugFoundDl)
                    {
                        return;
                    }

                    nekara.Api.ContextSwitch();
                    counter--;

                    nekara.Api.ContextSwitch();
                    Release(false, 1);

                    nekara.Api.ContextSwitch();
                    Release(false, 0);
                });

                var t3 = Nekara.Models.Task.WhenAll(t1, t2);

                t3.Wait();
                nekara.Api.WaitForMainTask();

                lck1 = false;
                lck2 = false;
                nekara.Api.CreateSession();
            }

            // Console.WriteLine("  ... Finished Deadlock Benchmark");
        }


        internal void Acquire(bool flag, int rid)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            Console.WriteLine("Acquire()");
            nekara.Api.ContextSwitch();

            if (rid == 0)
            {
                while (true)
                {
                    if (!flag)
                    {
                        if (lck1)
                        {
                            bugFoundDl = true;
                            break;
                        }
                    }

                    if (lck1 == false)
                    {
                        lck1 = true;
                        break;
                    }
                    else
                    {
                        nekara.Api.BlockedOnResource(rid);
                        continue;
                    }
                }
            }
            else
            {
                if (flag)
                {
                    if (lck2)
                    {
                        bugFoundDl = true;
                        return;
                    }
                }

                while (true)
                {
                    if (lck2 == false)
                    {
                        lck2 = true;
                        break;
                    }
                    else
                    {
                        nekara.Api.BlockedOnResource(rid);
                        continue;
                    }
                }
            }
        }

        internal void Release(bool flag, int rid)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            Console.WriteLine("Release()");
            // nekara.Api.Assert(lck1 == true, "Release called on non-acquired lock");

            if (rid == 0)
            {
                lck1 = false;
            }
            else
            {
                lck2 = false;
            }

            // lck1 = false;
            if (!bugFoundDl)
            {
                nekara.Api.SignalUpdatedResource(rid);
            }
        }
    }
}
