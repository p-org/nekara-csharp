using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;
using System;

namespace Nekara.Tests.Benchmarks
{
    class NestedTask1
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;
        static Lock lck;
        static int x;

        [TestMethod]
        public async static NativeTasks.Task Execute()
        {
            // initialize all relevant state
            
            lck = new Lock(0);
            x = 0;

            Console.WriteLine("Calling Foo");
            await Foo();
            Console.WriteLine("Returned from Foo");
            // await Bar();
            return;
        }

        public async static Task Foo()
        {
            // await Delay();

            Console.WriteLine("Inside Foo, Calling WrappedDelay");
            await WrappedDelay();
            Console.WriteLine("Inside Foo, Returned from WrappedDelay");

            /*lck.Acquire();

            nekara.ContextSwitch();
            int x1 = x;
            
            nekara.ContextSwitch();
            int x2 = x;

            lck.Release();

            nekara.ContextSwitch();
            nekara.Assert(x1 == x2, "Race!");

            await NativeTasks.Task.Delay(100);*/

            return;
        }

        public async static Task Bar()
        {
            lck.Acquire();

            nekara.ContextSwitch();

            x++;

            nekara.ContextSwitch();

            lck.Release();

            await NativeTasks.Task.Delay(100);

            return;
        }

        public async static NativeTasks.Task Delay()
        {
            Console.WriteLine("native delay enter");
            await NativeTasks.Task.Delay(1000);
            
            Console.WriteLine("native delay exit");
            return;
        }

        public async static Task WrappedDelay()
        {
            Console.WriteLine("Inside WrappedDelay, Calling Delay");
            await NativeTasks.Task.Delay(1000);

            Console.WriteLine("Inside WrappedDelay, Returned form Delay");
            return;
        }
    }
}
