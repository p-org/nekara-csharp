using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskAwaitTests
    {
        private static NekaraManagedClient nekara = RuntimeEnvironment.Client;

        private async Task WriteAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            entry.Value = value;
        }

        private async Task WriteWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            entry.Value = value;
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await WriteAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await WriteWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private async Task NestedWriteAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            await WriteAsync(entry, value);
        }

        private async Task NestedWriteWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            await WriteWithDelayAsync(entry, value);
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitNestedSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await NestedWriteAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitNestedAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await NestedWriteWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            //nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private async Task<int> GetWriteResultAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            entry.Value = value;
            return entry.Value;
        }

        private async Task<int> GetWriteResultWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            entry.Value = value;
            return entry.Value;
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await GetWriteResultAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await GetWriteResultWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private async Task<int> NestedGetWriteResultAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            return await GetWriteResultAsync(entry, value);
        }

        private async Task<int> NestedGetWriteResultWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            return await GetWriteResultWithDelayAsync(entry, value);
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitNestedSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await NestedGetWriteResultAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestAwaitNestedAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await NestedGetWriteResultWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
