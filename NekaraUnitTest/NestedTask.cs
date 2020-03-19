using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using NativeTasks = System.Threading.Tasks;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class NestedTask
    {
        [Fact(Timeout = 5000)]
        public async static NativeTasks.Task RunOne()
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

        [Fact(Timeout = 5000)]
        public async static NativeTasks.Task RunTwo()
        {
            await Foo_1();
            return;
        }

        public async static Task Foo_1()
        {
            await Bar_1();
            return;
        }

        public async static Task Bar_1()
        {
            await NativeTasks.Task.Delay(100);
            return;
        }
    }
}
