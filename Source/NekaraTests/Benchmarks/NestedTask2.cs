using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using Nekara.Models;
using Nekara.Core;

namespace Nekara.Tests.Benchmarks
{
    class NestedTask2
    {
        static ITestingService nekara = RuntimeEnvironment.Client.Api;

        [TestMethod]
        public async static NativeTasks.Task Execute()
        {
            await Foo(5);
            return;
        }

        public async static Task Foo(int count)
        {
            if (count == 0) return;
            await Foo(count - 1);
            return;
        }
    }
}
