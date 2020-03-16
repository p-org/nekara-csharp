using System;
using Xunit;
using Xunit.Abstractions;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Account
    {
        static int balance;

        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [Fact(Timeout = 5000)]
        public void AccountTest()
        {

            int x = 1;
            int y = 2;
            int z = 4;
            int balance = x;

            bool depositDone = false;
            bool withdrawDone = false;

            var l = new Lock(1);

            Task t1 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    if (depositDone && withdrawDone)
                    {
                        nekara.Assert(balance == (x - y) - z, "Bug found!");
                    }
                }
            });

            Task t2 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    balance += y;
                    depositDone = true;
                }
            });

            Task t3 = Task.Run(() =>
            {
                nekara.ContextSwitch();
                using (l.Acquire())
                {
                    balance -= z;
                    withdrawDone = true;
                }
            });

            Task.WaitAll(t1, t2, t3);

            return;
        }

        [Fact(Timeout = 5000)]
        public void AccountMinimalRunWithApi()
        {
            balance = 0;

            nekara.CreateTask();
            var t1 = System.Threading.Tasks.Task.Run(() =>
            {
                nekara.StartTask(1);
                TransactWithApi(100);
                nekara.EndTask(1);
            });

            nekara.CreateTask();
            var t2 = System.Threading.Tasks.Task.Run(() =>
            {
                nekara.StartTask(2);
                TransactWithApi(200);
                nekara.EndTask(2);
            });

            Task.Run(() => System.Threading.Tasks.Task.WaitAll(t1, t2));

            nekara.Assert(balance == 300, $"Bug Found! Balance does not equal 300 - it is {balance}");
        }

        [Fact(Timeout = 5000)]
        public void AccountMinimalRunWithTask()
        {
            balance = 0;

            var t1 = Task.Run(() => TransactWithApi(100));
            var t2 = Task.Run(() => TransactWithApi(200));

            Task.WaitAll(t1, t2);
            nekara.Assert(balance == 300, $"Bug Found! Balance does not equal 300 - it is {balance}");
        }

        static void Transact(int amount)
        {
            //Monitor.Enter(balance);
            int current = balance;
            balance = current + amount;
            //Monitor.Exit(balance);
        }

        static void TransactWithApi(int amount)
        {
            int current = balance;
            nekara.ContextSwitch();
            balance = current + amount;
        }
    }
}
