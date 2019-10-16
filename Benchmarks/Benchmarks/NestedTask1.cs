using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncTester.Client;

namespace Benchmarks.Benchmarks
{
    class NestedTask1
    {
        static TestingServiceProxy ts;
        static IAsyncLock lck;

        [TestMethod]
        public async static void Execute(TestingServiceProxy ts)
        {
            // initialize all relevant state
            NestedTask1.ts = ts;

            lck = ts.LockFactory.CreateLock(0);
            
            int x = 0;

            ts.Api.CreateTask();
            var t = Task.Run(() => {
                ts.Api.StartTask(1);
                // ts.Api.ContextSwitch();
                Console.WriteLine("    Hello");
                ts.Api.ContextSwitch();
                ts.Api.EndTask(1);
            });

            // ts.Api.ContextSwitch();
            Console.WriteLine("Hello");
            // ts.Api.ContextSwitch();

            await t;
        }
    }
}
