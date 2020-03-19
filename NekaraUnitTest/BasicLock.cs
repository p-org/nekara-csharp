using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using System.Threading.Tasks;
// using Nekara.Models;
using NekaraUnitTest.Common;
using System.Linq;

namespace NekaraUnitTest
{
    public class BasicLock
    {
        public static bool lck;

        [Fact(Timeout = 5000)]
        public static void RunTwo()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 2).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunThree()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 3).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunFour()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 4).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));

            nekara.Api.WaitForMainTask();
        }

        [Fact(Timeout = 5000)]
        public static void RunFive()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            nekara.Api.CreateResource(0);
            lck = false;
            Enumerable.Range(1, 5).ToList().ForEach(i => Nekara.Models.Task.Run(() => LockContender(i)));

            nekara.Api.WaitForMainTask();
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
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            Console.WriteLine("Task {0}: Acquire()", taskId);
            nekara.Api.ContextSwitch();
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
                    nekara.Api.BlockedOnResource(0);
                    //nekara.ContextSwitch();
                    continue;
                }
            }
        }

        static void Release(int taskId)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            Console.WriteLine("Task {0}: Release()", taskId);
            nekara.Api.Assert(lck == true, "Release called on non-acquired lock");

            //nekara.ContextSwitch();
            lck = false;
            //nekara.ContextSwitch();
            nekara.Api.SignalUpdatedResource(0);
            //nekara.ContextSwitch();
        }
    }
}
