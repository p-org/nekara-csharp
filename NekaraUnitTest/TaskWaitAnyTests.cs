using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskWaitAnyTests
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
        public void TestWhenAnyWithTwoSynchronousTasks()
        {
            SharedEntry entry = new SharedEntry();
            Task task1 = WriteAsync(entry, 5);
            Task task2 = WriteAsync(entry, 3);
            Task.WaitAny(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted || task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoAsynchronousTasks()
        {
            SharedEntry entry = new SharedEntry();
            Task task1 = WriteWithDelayAsync(entry, 3);
            Task task2 = WriteWithDelayAsync(entry, 5);
            Task.WaitAny(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted || task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);
        }

        [Fact(Timeout = 5000)]
        public void TestWhenAnyWithTwoParallelTasks()
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

            Task.WaitAny(task1, task2);

            nekara.Api.WaitForMainTask();

            // nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted || task2.IsCompleted);
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

        /* [Fact(Timeout = 5000)]
        public static void TestWhenAnyWithTwoSynchronousTaskResults()
        {
            Task<int> task1 = GetWriteResultAsync(5);
            Task<int> task2 = GetWriteResultAsync(3);
            int index = Task.WaitAny(task1, task2);
            nekara.Api.Assert(index >= 0, "Index is negative.");
            nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            nekara.Api.Assert((index == 0 && task1.Result == 5) || (index == 1 && task2.Result == 3), "Found unexpected value.");
        }

        [Fact(Timeout = 5000)]
        public static void TestWhenAnyWithTwoAsynchronousTaskResults()
        {
            Task<int> task1 = GetWriteResultWithDelayAsync(5);
            Task<int> task2 = GetWriteResultWithDelayAsync(3);
            int index = Task.WaitAny(task1, task2);
            nekara.Api.Assert(index >= 0, "Index is negative.");
            nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            nekara.Api.Assert((index == 0 && task1.Result == 5) || (index == 1 && task2.Result == 3), "Found unexpected value.");
        }

        [Fact(Timeout = 5000)]
        public static void TestWhenAnyWithTwoParallelSynchronousTaskResults()
        {
            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(3);
            });

            int index = Task.WaitAny(task1, task2);

            nekara.Api.Assert(index >= 0, "Index is negative.");
            nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            nekara.Api.Assert((index == 0 && task1.Result == 5) || (index == 1 && task2.Result == 3), "Found unexpected value.");
        }

        [Fact(Timeout = 5000)]
        public static void TestWhenAnyWithTwoParallelAsynchronousTaskResults()
        {
            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(3);
            });

            int index = Task.WaitAny(task1, task2);

            nekara.Api.Assert(index >= 0, "Index is negative.");
            nekara.Api.Assert(task1.IsCompleted || task2.IsCompleted, "No task has completed.");
            nekara.Api.Assert((index == 0 && task1.Result == 5) || (index == 1 && task2.Result == 3), "Found unexpected value.");
        } */

    }
}
