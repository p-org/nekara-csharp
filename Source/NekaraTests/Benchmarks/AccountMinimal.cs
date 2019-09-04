using System.Threading.Tasks;
using Nekara.Core;
using Nekara.Client;
using System.Threading;

namespace Nekara.Tests.Benchmarks
{
    class AccountMinimal
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        static int balance;

        [TestMethod]
        static void RunVanilla()
        {
            balance = 0;

            var t1 = Task.Run(() => Transact(100));

            var t2 = Task.Run(() => Transact(200));

            Models.Task.Run(() => Task.WaitAll(t1, t2));

            nekara.Assert(balance == 300, $"Bug Found! Balance does not equal 300 - it is {balance}");
        }

        [TestMethod]
        static void RunWithApi()
        {
            balance = 0;

            nekara.CreateTask();
            var t1 = Task.Run(() =>
            {
                nekara.StartTask(1);
                TransactWithApi(100);
                nekara.EndTask(1);
            });

            nekara.CreateTask();
            var t2 = Task.Run(() =>
            {
                nekara.StartTask(2);
                TransactWithApi(200);
                nekara.EndTask(2);
            });

            Models.Task.Run(() => Task.WaitAll(t1, t2));

            nekara.Assert(balance == 300, $"Bug Found! Balance does not equal 300 - it is {balance}");
        }

        [TestMethod]
        static void RunWithTask()
        {
            balance = 0;

            var t1 = Models.Task.Run(() => TransactWithApi(100));

            var t2 = Models.Task.Run(() => TransactWithApi(200));

            Models.Task.WaitAll(t1, t2);

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
