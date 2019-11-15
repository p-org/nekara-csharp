using System;
using System.Linq;
using System.Threading.Tasks;
using Nekara.Client;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class BasicLock
    {
        public static ITestingService nekara = RuntimeEnvironment.Client.Api;

        public static bool lck;

        [TestMethod]
        public static void RunTwo()
        {
            nekara.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 2).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));
        }

        [TestMethod]
        public static void RunThree()
        {
            nekara.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 3).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));
        }

        [TestMethod]
        public static void RunFour()
        {
            nekara.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 4).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));
        }

        [TestMethod]
        public static void RunFive()
        {
            nekara.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 5).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));
        }

        public static void LockContender(int i)
        {
            Acquire(i);
            Console.WriteLine("Task {0} acquired lock - lock value: {1}", i, lck);
            Release(i);
            Console.WriteLine("Task {0} released lock - lock value: {1}", i, lck);
        }

        static void Acquire(int taskId)
        {
            Console.WriteLine("Task {0}: Acquire()", taskId);
            nekara.ContextSwitch();
            while (true)
            {
                if (lck == false)
                {
                    //nekara.ContextSwitch();
                    lck = true;
                    Console.WriteLine("Task {0}: Inside Acquire, critical path - lock value: {1}", taskId, lck);
                    //nekara.ContextSwitch();
                    break;
                }
                else
                {
                    //nekara.ContextSwitch();
                    Console.WriteLine("Task {0}: Inside Acquire, blocked on the lock - lock value: {1}", taskId, lck);
                    nekara.BlockedOnResource(0);
                    //nekara.ContextSwitch();
                    continue;
                }
            }
        }

        static void Release(int taskId)
        {
            Console.WriteLine("Task {0}: Release()", taskId);
            nekara.Assert(lck == true, "Release called on non-acquired lock");

            //nekara.ContextSwitch();
            lck = false;
            //nekara.ContextSwitch();
            nekara.SignalUpdatedResource(0);
            //nekara.ContextSwitch();
        }
    }
}
