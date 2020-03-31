﻿using System;
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
        public static void RunBasic()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            Task.Run(() => Foo());

            nekara.CreateTask();
            Task.Run(() => Bar());
        }

        [TestMethod]
        public static void RunBlocking()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.CreateTask();
            var t2 = Task.Run(() => Bar());

            nekara.CreateTask();
            var all = Task.Run(() => {
                nekara.StartTask(4);
                Task.WhenAll(t1, t2).Wait();
                nekara.EndTask(4);
            });
        }

        [TestMethod]
        public static Task RunBasicTask()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.CreateTask();
            var t2 = Task.Run(() => Bar());

            return Task.WhenAll(t1, t2);
        }

        [TestMethod]
        public static Task RunBlockingTask()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.CreateTask();
            var t2 = Task.Run(() => Bar());

            Task.WhenAll(t1, t2).Wait();

            return Task.CompletedTask;
        }

        [TestMethod]
        public async static Task RunBlockingAsync()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            var t1 = Task.Run(() => Foo());

            nekara.CreateTask();
            var t2 = Task.Run(() => Bar());

            await Task.WhenAll(t1, t2);

            return;
        }

        [TestMethod]
        public static void RunLiveLock()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            Task.Run(() => Foo());

            nekara.CreateTask();
            Task.Run(() => Bar());

            Task.Run(() => Distraction());  // this is an undeclared Task, so we should expect the server to fail.
        }

        [TestMethod]
        public static void RunLiveLockTrivial()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            Task.Run(() => {
                nekara.StartTask(1);

                nekara.ContextSwitch();

                nekara.EndTask(1);
            });

            Task.Run(() =>
            {
                nekara.ContextSwitch();
            });
        }

        [TestMethod]
        public static void RunUserMistake()
        {
            // initialize all relevant state
            nekara.CreateResource(0);
            lck = false;
            x = 0;

            nekara.CreateTask();
            Task.Run(() => Foo());

            nekara.CreateTask();
            Task.Run(() => Bar());

            Task.Run(() => UndeclaredTask());  // this is an undeclared Task, so we should expect the server to fail.
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
            Acquire();

            nekara.ContextSwitch();
            x = 1;

            Release();

            Console.WriteLine("Bar EndTask");
            nekara.EndTask(2);
        }

        static void Distraction()
        {
            // nekara.StartTask(3);

            nekara.ContextSwitch();
        }

        static void UndeclaredTask()
        {
            nekara.StartTask(3);

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
