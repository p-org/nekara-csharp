using System;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class DeadlockWithNekaraLock2
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        static int x = 0;
        static Nekara.Models.Lock lck;

        [TestMethod]
        public static void Run()
        {
            // initialize all relevant state
            lck = new Nekara.Models.Lock(0);
            x = 0;

            nekara.CreateTask();
            Task.Run(() => Foo(1));

            nekara.CreateTask();
            Task.Run(() => Foo(2));

            nekara.CreateTask();
            Task.Run(() => Foo(3));
        }

        static void Foo(int taskId)
        {
            nekara.StartTask(taskId);

            Console.WriteLine("Foo({0})/Acquire()", taskId);
            lck.Acquire();

            Console.WriteLine("Foo({0})/ContextSwitch():0", taskId);
            nekara.ContextSwitch();
            x = taskId;

            Console.WriteLine("Foo({0})/ContextSwitch():1", taskId);
            nekara.ContextSwitch();
            int lx1 = x;

            Console.WriteLine("Foo({0})/ContextSwitch():2", taskId);
            nekara.ContextSwitch();
            int lx2 = x;

            Console.WriteLine("Foo({0})/Release()", taskId);
            if (taskId != 1) lck.Release();

            nekara.Assert(lx1 == lx2, "Race!");

            Console.WriteLine("Foo({0})/EndTask()", taskId);
            nekara.EndTask(taskId);
        }
    }
}
