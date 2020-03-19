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

        private static async Task WriteAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            entry.Value = value;
        }

        private static async Task WriteWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            entry.Value = value;
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await WriteAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await WriteWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private static async Task NestedWriteAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            await WriteAsync(entry, value);
        }

        private static async Task NestedWriteWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            await WriteWithDelayAsync(entry, value);
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitNestedSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await NestedWriteAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitNestedAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await NestedWriteWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private static async Task<int> GetWriteResultAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            entry.Value = value;
            return entry.Value;
        }

        private static async Task<int> GetWriteResultWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            entry.Value = value;
            return entry.Value;
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await GetWriteResultAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await GetWriteResultWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private static async Task<int> NestedGetWriteResultAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            return await GetWriteResultAsync(entry, value);
        }

        private static async Task<int> NestedGetWriteResultWithDelayAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            return await GetWriteResultWithDelayAsync(entry, value);
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitNestedSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await NestedGetWriteResultAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitNestedAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await NestedGetWriteResultWithDelayAsync(entry, 5);

            nekara.Api.WaitForMainTask();
            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
