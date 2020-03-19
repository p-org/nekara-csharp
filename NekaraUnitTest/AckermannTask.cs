using System;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NativeTasks = System.Threading.Tasks;

namespace NekaraUnitTest
{
    public class AckermannTask
    {
        private static NekaraManagedClient nekara = RuntimeEnvironment.Client;

        [Fact(Timeout = 5000)]
        public async static NativeTasks.Task AckermannTestRun()
        {
            int m = 2;
            int n = 2;

            int answer = await Ackermann(m, n);

            Console.WriteLine($"Ackerman({m}, {n}) = {answer}");

            nekara.Api.WaitForMainTask();

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
