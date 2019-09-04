using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;
using System;

namespace Nekara.Tests.Benchmarks
{
    class AckermannTask
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [TestMethod]
        public async static NativeTasks.Task Run()
        {
            int m = 2;
            int n = 2;

            int answer = await Ackermann(m, n);

            Console.WriteLine($"Ackerman({m}, {n}) = {answer}");
            return;
        }

        public async static Task<int> Ackermann(int m, int n)
        {
            if (m == 0) return n + 1;
            if (m > 0)
            {
                if (n == 0) return await Ackermann(m - 1, 1);
                if (n > 0) return await Ackermann(m - 1, await Ackermann(m, n - 1));
            }

            throw new Exception($"Invalid arguments {m} and {n}");
        }
    }
}
