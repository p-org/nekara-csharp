using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskRunTests
    {
        static NekaraManagedClient nekara = RuntimeEnvironment.Client;

        [Fact(Timeout = 5000)]
        public async Task TestRunParallelTask()
        {

            SharedEntry entry = new SharedEntry();
            await Task.Run(() =>
            {
                entry.Value = 5;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestRunParallelSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await Task.Run(async () =>
            {
                await Task.CompletedTask;
                entry.Value = 5;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestRunParallelAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await Task.Run(async () =>
            {
                await Task.Delay(1);
                entry.Value = 5;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestRunNestedParallelSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await Task.Run(async () =>
            {
                await Task.Run(async () =>
                {
                    await Task.CompletedTask;
                    entry.Value = 3;
                });

                entry.Value = 5;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitNestedParallelAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await Task.Run(async () =>
            {
                await Task.Run(async () =>
                {
                    await Task.Delay(1);
                    entry.Value = 3;
                });

                entry.Value = 5;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }


        [Fact(Timeout = 5000)]
        public async Task TestRunParallelSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await Task.Run(async () =>
            {
                await Task.CompletedTask;
                entry.Value = 5;
                return entry.Value;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestRunParallelAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await Task.Run(async () =>
            {
                await Task.Delay(1);
                entry.Value = 5;
                return entry.Value;
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestRunNestedParallelSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await Task.Run(async () =>
            {
                return await Task.Run(async () =>
                {
                    await Task.CompletedTask;
                    entry.Value = 5;
                    return entry.Value;
                });
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestRunNestedParallelAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await Task.Run(async () =>
            {
                return await Task.Run(async () =>
                {
                    await Task.Delay(1);
                    entry.Value = 5;
                    return entry.Value;
                });
            });


            nekara.Api.WaitForMainTask();
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
