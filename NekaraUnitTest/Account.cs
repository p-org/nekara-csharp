using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class Account
    {
        static int balance;

        [Fact(Timeout = 5000)]
        public void AccountTest()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            int x = 1;
            int y = 2;
            int z = 4;
            int balance = x;

            bool depositDone = false;
            bool withdrawDone = false;

            var l = new Lock(1);

            Task t1 = Task.Run(() =>
            {
                nekara.Api.ContextSwitch();
                using (l.Acquire())
                {
                    if (depositDone && withdrawDone)
                    {
                        bool _m1 = balance == ((x - y) - z);
                        nekara.Api.Assert(_m1, "Bug found!");
                    }
                }
            });

            Task t2 = Task.Run(() =>
            {
                nekara.Api.ContextSwitch();
                using (l.Acquire())
                {
                    balance += y;
                    depositDone = true;
                }
            });

            Task t3 = Task.Run(() =>
            {
                nekara.Api.ContextSwitch();
                using (l.Acquire())
                {
                    balance -= z;
                    withdrawDone = true;
                }
            });

            Task.WaitAll(t1, t2, t3);

            nekara.Api.WaitForMainTask();

            return;
        }

        [Fact(Timeout = 5000)]
        public void AccountMinimalRunWithApi()
        {

            bool bugfound = false;

            while (!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                balance = 0;
                nekara.Api.CreateTask();
                var t1 = System.Threading.Tasks.Task.Run(() =>
                {
                    nekara.Api.StartTask(1);
                    TransactWithApi(100);
                    nekara.Api.EndTask(1);
                });

                nekara.Api.CreateTask();
                var t2 = System.Threading.Tasks.Task.Run(() =>
                {
                    nekara.Api.StartTask(2);
                    TransactWithApi(200);
                    nekara.Api.EndTask(2);
                });

                Task.Run(() => System.Threading.Tasks.Task.WaitAll(t1, t2));

                nekara.Api.WaitForMainTask();

                if (balance != 300)
                {
                    bugfound = true;
                }
            }
            Assert.True(bugfound);

            // nekara.Api.Assert(balance == 300, $"Bug Found! Balance does not equal 300 - it is {balance}");
        }

        [Fact(Timeout = 5000)]
        public void AccountMinimalRunWithTask()
        {
            bool bugfound = false;

            while(!bugfound)
            {
                NekaraManagedClient nekara = RuntimeEnvironment.Client;
                nekara.Api.CreateSession();

                balance = 0;

                var t1 = Task.Run(() => TransactWithApi(100));
                var t2 = Task.Run(() => TransactWithApi(200));

                Task.WaitAll(t1, t2);
                nekara.Api.WaitForMainTask();

                if (balance != 300)
                {
                    bugfound = true;
                }
            }

            Assert.True(bugfound);

            // nekara.Api.Assert(balance == 300, $"Bug Found! Balance does not equal 300 - it is {balance}"); 
        }

        static void TransactWithApi(int amount)
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;

            int current = balance;
            nekara.Api.ContextSwitch();
            balance = current + amount;
        }
    }
}
