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

        [TestMethod]
        public async static NativeTasks.Task Execute()
        {
            await Foo();
            // await Bar();
            return;
        }

        public async static Task Foo()
        {
            await Bar();
            return;
        }

        public async static Task Bar()
        {
            await NativeTasks.Task.Delay(100);
            return;
        }
    }
}
