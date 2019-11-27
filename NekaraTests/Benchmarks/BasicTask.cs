using System;
using System.Threading;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class BasicTask
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [TestMethod]
        public static void RunOne()
        {
            nekara.CreateTask();
            nekara.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented());
        }

        [TestMethod]
        public static void RunOneBlocking()
        {
            nekara.CreateTask();
            nekara.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented());
            nekara.ContextSwitch();
            if (t1.Status != TaskStatus.RanToCompletion) nekara.BlockedOnResource(1000);
            t1.Wait();
        }

        [TestMethod]
        public static void RunMultiple()
        {
            Console.WriteLine("Running {0} in AppDomain {1}", RuntimeEnvironment.SessionKey.Value, AppDomain.CurrentDomain.FriendlyName);

            nekara.CreateTask();
            nekara.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented(1));

            nekara.CreateTask();
            nekara.CreateResource(2000);
            var t2 = Task.Run(() => FooInstrumented(2));

            nekara.CreateTask();
            nekara.CreateResource(3000);
            var t3 = Task.Run(() => FooInstrumented(3));
        }

        [TestMethod]
        public static void RunMultipleBlocking()
        {
            nekara.CreateTask();
            nekara.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented(1));

            nekara.CreateTask();
            nekara.CreateResource(2000);
            var t2 = Task.Run(() => FooInstrumented(2));

            nekara.CreateTask();
            nekara.CreateResource(3000);
            var t3 = Task.Run(() => FooInstrumented(3));

            if (t1.Status != TaskStatus.RanToCompletion) nekara.BlockedOnResource(1000);
            if (t2.Status != TaskStatus.RanToCompletion) nekara.BlockedOnResource(2000);
            if (t3.Status != TaskStatus.RanToCompletion) nekara.BlockedOnResource(3000);

            Task.WaitAll(t1, t2, t3);
        }

        [TestMethod]
        public static void RunMultipleAnyBlocking()
        {
            nekara.CreateTask();
            nekara.CreateResource(1000);
            var t1 = Task.Run(() => FooInstrumented(1));

            nekara.CreateTask();
            nekara.CreateResource(2000);
            var t2 = Task.Run(() => FooInstrumented(2));

            nekara.CreateTask();
            nekara.CreateResource(3000);
            var t3 = Task.Run(() => FooInstrumented(3));

            if (t1.Status != TaskStatus.RanToCompletion
                && t2.Status != TaskStatus.RanToCompletion
                && t3.Status != TaskStatus.RanToCompletion) nekara.BlockedOnAnyResource(1000, 2000, 3000);

            Task.WaitAny(t1, t2, t3);
        }

        [TestMethod]
        public static void RunOneControlled()
        {
            var t1 = Nekara.Models.Task.Run(() => Foo());
        }

        [TestMethod]
        public static void RunOneControlledBlocking()
        {
            var t1 = Nekara.Models.Task.Run(() => Foo());
            t1.Wait();
        }

        [TestMethod]
        public async static void RunOneControlledAsync()
        {
            var t1 = Nekara.Models.Task.Run(() => Foo());
            await t1;
        }

        [TestMethod]
        public static void RunOneControlledGeneric()
        {
            var t1 = Nekara.Models.Task<int>.Run(Bar);
        }

        [TestMethod]
        public static void RunOneControlledGenericBlocking()
        {
            var t1 = Nekara.Models.Task<int>.Run(Bar);
            t1.Wait();
        }

        [TestMethod]
        public async static void RunOneControlledGenericAsync()
        {
            var t1 = Nekara.Models.Task<int>.Run(Bar);
            await t1;
        }

        [TestMethod]
        public static void RunMultipleControlledBlocking()
        {
            Console.WriteLine("Running {0}", RuntimeEnvironment.SessionKey.Value);

            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            Nekara.Models.Task.WaitAll(t1, t2, t3);
        }

        [TestMethod]
        public static void RunMultipleControlledAnyBlocking()
        {
            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            Nekara.Models.Task.WaitAny(t1, t2, t3);
        }

        [TestMethod(15000, 100)]
        public static void RunMultipleControlledBlockingTask()
        {
            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            var all = Nekara.Models.Task.WhenAll(t1, t2, t3);

            all.Wait();
        }

        [TestMethod]
        public static void RunMultipleControlledAnyBlockingTask()
        {
            var t1 = Nekara.Models.Task.Run(() => Foo());

            var t2 = Nekara.Models.Task.Run(() => Foo());

            var t3 = Nekara.Models.Task.Run(() => Foo());

            var any = Nekara.Models.Task.WhenAny(t1, t2, t3);

            any.Wait();
        }

        public static void FooInstrumented(int taskId = 1)
        {
            nekara.StartTask(taskId);
            //Console.WriteLine("FooInstrumented ContextSwitch");
            nekara.ContextSwitch();
            //Console.WriteLine("FooInstrumented ContextSwitch");
            nekara.ContextSwitch();
            //Console.WriteLine("FooInstrumented ContextSwitch");
            nekara.ContextSwitch();
            nekara.SignalUpdatedResource(taskId * 1000);
            nekara.DeleteResource(taskId * 1000);
            nekara.EndTask(taskId);
        }

        public static void Foo()
        {
            nekara.ContextSwitch();
        }

        public static int Bar()
        {
            nekara.ContextSwitch();
            return 1;
        }
    }
}
