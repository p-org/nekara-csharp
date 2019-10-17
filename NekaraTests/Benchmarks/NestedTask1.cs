using System;
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks.Benchmarks
{
    class NestedTask1
    {
        static ITestingService nekara;
        static Lock lck;

        [TestMethod]
        public async static void Execute()
        {
            // initialize all relevant state
            NestedTask1.nekara = RuntimeEnvironment.Client.Api;

            lck = new Lock(0);
            
            int x = 0;

            var t = Task.Run(() => {
                // ts.Api.ContextSwitch();
                Console.WriteLine("    Hello");
                nekara.ContextSwitch();
            });

            // ts.Api.ContextSwitch();
            Console.WriteLine("Hello");
            // ts.Api.ContextSwitch();

            await t;
        }
    }
}
