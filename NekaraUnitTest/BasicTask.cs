using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using System.Threading.Tasks;
// using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class BasicTask
    {
        [Fact(Timeout = 5000)]
        public static void RunOne()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented());

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunOneBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented());
            nekara.Api.ContextSwitch();
            if (t1.Status != TaskStatus.RanToCompletion) nekara.Api.BlockedOnResource(1000);
            t1.Wait();

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultiple()
        {
            // Console.WriteLine("Running {0} in AppDomain {1}", RuntimeEnvironment.SessionKey.Value, AppDomain.CurrentDomain.FriendlyName);
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented(1));

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(2000);
            var t2 = Task.Run(() => FooInstrumented(2));

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(3000);
            var t3 = Task.Run(() => FooInstrumented(3));

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultipleBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented(1));

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(2000);
            var t2 = Task.Run(() => FooInstrumented(2));

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(3000);
            var t3 = Task.Run(() => FooInstrumented(3));

            if (t1.Status != TaskStatus.RanToCompletion) nekara.Api.BlockedOnResource(1000);
            if (t2.Status != TaskStatus.RanToCompletion) nekara.Api.BlockedOnResource(2000);
            if (t3.Status != TaskStatus.RanToCompletion) nekara.Api.BlockedOnResource(3000);

            Task.WaitAll(t1, t2, t3);

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultipleAnyBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented(1));

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(2000);
            var t2 = Task.Run(() => FooInstrumented(2));

            nekara.Api.CreateTask();
            nekara.Api.CreateResource(3000);
            var t3 = Task.Run(() => FooInstrumented(3));

            if (t1.Status != TaskStatus.RanToCompletion
                && t2.Status != TaskStatus.RanToCompletion
                && t3.Status != TaskStatus.RanToCompletion) nekara.Api.BlockedOnAnyResource(1000, 2000, 3000);

            Task.WaitAny(t1, t2, t3);

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunOneControlled()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task.Run(() => Foo());

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunOneControlledBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task.Run(() => Foo());
            t1.Wait();

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public async static void RunOneControlledAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task.Run(() => Foo());
            await t1;

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunOneControlledGeneric()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task<int>.Run(Bar);

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunOneControlledGenericBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task<int>.Run(Bar);
            t1.Wait();

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public async static void RunOneControlledGenericAsync()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task<int>.Run(Bar);
            await t1;

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultipleControlledBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            // Console.WriteLine("Running {0}", RuntimeEnvironment.SessionKey.Value);

            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            Nekara.Models.Task.WaitAll(t1, t2, t3);

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultipleControlledAnyBlocking()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            Nekara.Models.Task.WaitAny(t1, t2, t3);

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultipleControlledBlockingTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            var all = Nekara.Models.Task.WhenAll(t1, t2, t3);

            all.Wait();

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunMultipleControlledAnyBlockingTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            var any = Nekara.Models.Task.WhenAny(t1, t2, t3);

            any.Wait();

            nekara.Api.WaitForMainTask();
        }

        public static void FooInstrumented(int taskId = 1)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.StartTask(taskId);
            //Console.WriteLine("FooInstrumented ContextSwitch");
            nekara.Api.ContextSwitch();
            //Console.WriteLine("FooInstrumented ContextSwitch");
            nekara.Api.ContextSwitch();
            //Console.WriteLine("FooInstrumented ContextSwitch");
            nekara.Api.ContextSwitch();
            nekara.Api.SignalUpdatedResource(taskId * 1000);
            nekara.Api.DeleteResource(taskId * 1000);
            nekara.Api.EndTask(taskId);
        }

        public static void Foo()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.ContextSwitch();
        }

        public static int Bar()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            nekara.Api.ContextSwitch();
            return 1;
        }
    }
}
