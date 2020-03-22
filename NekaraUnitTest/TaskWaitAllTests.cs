using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskWaitAllTests
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
        public void TestWaitAllWithTwoSynchronousTasks()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            SharedEntry entry = new SharedEntry();
            Task task1 = WriteAsync(entry, 5);
            Task task2 = WriteAsync(entry, 3);
            Task.WaitAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWaitAllWithTwoAsynchronousTasks()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            SharedEntry entry = new SharedEntry();
            Task task1 = WriteWithDelayAsync(entry, 3);
            Task task2 = WriteWithDelayAsync(entry, 5);
            Task.WaitAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWaitAllWithTwoParallelTasks()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            SharedEntry entry = new SharedEntry();

            Task task1 = Task.Run(async () =>
            {
                await WriteAsync(entry, 3);
            });

            Task task2 = Task.Run(async () =>
            {
                await WriteAsync(entry, 5);
            });

            Task.WaitAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");
            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);
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
        public void TestWaitAllWithTwoSynchronousTaskResults()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task<int> task1 = GetWriteResultAsync(5);
            Task<int> task2 = GetWriteResultAsync(3);
            Task.WaitAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(task1.Result == 5 && task2.Result == 3, "Found unexpected value.");
            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(task1.Result == 5 && task2.Result == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWaitAllWithTwoAsynchronousTaskResults()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task<int> task1 = GetWriteResultWithDelayAsync(5);
            Task<int> task2 = GetWriteResultWithDelayAsync(3);
            Task.WaitAll(task1, task2);
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(task1.Result == 5 && task2.Result == 3, "Found unexpected value.");
            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(task1.Result == 5 && task2.Result == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWaitAllWithTwoParallelSynchronousTaskResults()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(3);
            });

            Task.WaitAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(task1.Result == 5 && task2.Result == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(task1.Result == 5 && task2.Result == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWaitAllWithTwoParallelAsynchronousTaskResults()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(3);
            });

            Task.WaitAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(task1.Result == 5 && task2.Result == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(task1.Result == 5 && task2.Result == 3);
        }
    }
}
