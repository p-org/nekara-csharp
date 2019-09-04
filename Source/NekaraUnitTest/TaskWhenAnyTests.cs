using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskWhenAnyTests
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
        public async Task TestWhenAnyWithTwoSynchronousTasks()
        {
            SharedEntry entry = new SharedEntry();
            Task task1 = WriteAsync(entry, 5);
            Task task2 = WriteAsync(entry, 3);
            Task result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value."); */

            Assert.True(result.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithTwoAsynchronousTasks()
        {
            SharedEntry entry = new SharedEntry();
            Task task1 = WriteWithDelayAsync(entry, 3);
            Task task2 = WriteWithDelayAsync(entry, 5);
            Task result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(result.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithTwoParallelTasks()
        {
            SharedEntry entry = new SharedEntry();

            Task task1 = Task.Run(async () =>
            {
                await WriteAsync(entry, 3);
            });

            Task task2 = Task.Run(async () =>
            {
                await WriteAsync(entry, 5);
            });

            Task result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(result.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private async Task<int> GetWriteResultAsync(int value)
        {
            await Task.CompletedTask;
            return value;
        }

        private async Task<int> GetWriteResultWithDelayAsync(int value)
        {
            await Task.Delay(1);
            return value;
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithTwoSynchronousTaskResults()
        {
            Task<int> task1 = GetWriteResultAsync(5);
            Task<int> task2 = GetWriteResultAsync(3);
            Task<int> result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            nekara.Api.Assert(
                (result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3),
                "Found unexpected value."); */

            Assert.True(result.IsCompleted);
            Assert.True((result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3));

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithTwoAsynchronousTaskResults()
        {
            Task<int> task1 = GetWriteResultWithDelayAsync(5);
            Task<int> task2 = GetWriteResultWithDelayAsync(3);
            Task<int> result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            nekara.Api.Assert(
                (result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3),
                "Found unexpected value."); */

            Assert.True(result.IsCompleted);
            Assert.True((result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3));

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithTwoParallelSynchronousTaskResults()
        {
            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(3);
            });

            Task<int> result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            nekara.Api.Assert(
                (result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3),
                "Found unexpected value."); */

            Assert.True(result.IsCompleted);
            Assert.True((result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3));

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithTwoParallelAsynchronousTaskResults()
        {
            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(3);
            });

            Task<int> result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(result.IsCompleted, "No task has completed.");
            nekara.Api.Assert(
                (result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3),
                "Found unexpected value."); */

            Assert.True(result.IsCompleted);
            Assert.True((result.Id == task1.Id && result.Result == 5) ||
                (result.Id == task2.Id && result.Result == 3));

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAnyWithException()
        {
            SharedEntry entry = new SharedEntry();

            Task task1 = Task.Run(async () =>
            {
                await WriteAsync(entry, 3);
                throw new InvalidOperationException();
            });

            Task task2 = Task.Run(async () =>
            {
                await WriteAsync(entry, 5);
                throw new InvalidOperationException();
            });

            Task result = await Task.WhenAny(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(result.IsFaulted, "No task has faulted.");
            nekara.Api.Assert(result.Exception.InnerException.GetType() == typeof(InvalidOperationException),
                "The exception is not of the expected type."); */

            Assert.True(result.IsFaulted);
            Assert.True(result.Exception.InnerException.GetType() == typeof(InvalidOperationException));

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
