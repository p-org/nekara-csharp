﻿using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskWhenAllTests
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
        public async Task TestWhenAllWithTwoSynchronousTasks()
        {
            SharedEntry entry = new SharedEntry();
            Task task1 = WriteAsync(entry, 5);
            Task task2 = WriteAsync(entry, 3);
            await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "One task has not completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);
            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAllWithTwoAsynchronousTasks()
        {
            SharedEntry entry = new SharedEntry();
            Task task1 = WriteWithDelayAsync(entry, 3);
            Task task2 = WriteWithDelayAsync(entry, 5);
            await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");
            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(entry.Value == 5 || entry.Value == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAllWithTwoParallelTasks()
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

            await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            // nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            // nekara.Api.Assert(entry.Value == 5 || entry.Value == 3, "Found unexpected value.");

            Assert.True(task1.IsCompleted && task2.IsCompleted);
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
        public async Task TestWhenAllWithTwoSynchronousTaskResults()
        {
            Task<int> task1 = GetWriteResultAsync(5);
            Task<int> task2 = GetWriteResultAsync(3);
            int[] results = await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            nekara.Api.Assert(results.Length == 2, "Result count is not 2.");
            nekara.Api.Assert(results[0] == 5 && results[1] == 3, "Found unexpected value."); */

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(results.Length == 2);
            Assert.True(results[0] == 5 && results[1] == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAllWithTwoAsynchronousTaskResults()
        {
            Task<int> task1 = GetWriteResultWithDelayAsync(5);
            Task<int> task2 = GetWriteResultWithDelayAsync(3);
            int[] results = await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            nekara.Api.Assert(results.Length == 2, "Result count is not 2.");
            nekara.Api.Assert(results[0] == 5 && results[1] == 3, "Found unexpected value."); */

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(results.Length == 2);
            Assert.True(results[0] == 5 && results[1] == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAllWithTwoParallelSynchronousTaskResults()
        {
            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultAsync(3);
            });

            int[] results = await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            nekara.Api.Assert(results.Length == 2, "Result count is not 2.");
            nekara.Api.Assert(results[0] == 5 && results[1] == 3, "Found unexpected value."); */

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(results.Length == 2);
            Assert.True(results[0] == 5 && results[1] == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAllWithTwoParallelAsynchronousTaskResults()
        {
            Task<int> task1 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(5);
            });

            Task<int> task2 = Task.Run(async () =>
            {
                return await GetWriteResultWithDelayAsync(3);
            });

            int[] results = await Task.WhenAll(task1, task2);

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(task1.IsCompleted && task2.IsCompleted, "At least one task has not completed.");
            nekara.Api.Assert(results.Length == 2, "Result count is not 2.");
            nekara.Api.Assert(results[0] == 5 && results[1] == 3, "Found unexpected value."); */

            Assert.True(task1.IsCompleted && task2.IsCompleted);
            Assert.True(results.Length == 2);
            Assert.True(results[0] == 5 && results[1] == 3);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestWhenAllWithException()
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
                throw new NotSupportedException();
            });

            try
            {
                await Task.WhenAll(task1, task2);
            }
            catch (AggregateException ex)
            {
                /* nekara.Api.Assert(ex.InnerExceptions.Count == 2, "Expected two exceptions.");
                nekara.Api.Assert(ex.InnerExceptions[0].InnerException.GetType() == typeof(InvalidOperationException),
                    "The first exception is not of the expected type.");
                nekara.Api.Assert(ex.InnerExceptions[1].InnerException.GetType() == typeof(NotSupportedException),
                    "The second exception is not of the expected type."); */

                Assert.True(ex.InnerExceptions.Count == 2);
                Assert.True(ex.InnerExceptions[0].InnerException.GetType() == typeof(InvalidOperationException));
                Assert.True(ex.InnerExceptions[1].InnerException.GetType() == typeof(NotSupportedException));
            }

            nekara.Api.WaitForMainTask();
            /* nekara.Api.Assert(task1.IsFaulted && task2.IsFaulted, "One task has not faulted.");
            nekara.Api.Assert(task1.Exception.InnerException.GetType() == typeof(InvalidOperationException),
                "The first task exception is not of the expected type.");
            nekara.Api.Assert(task2.Exception.InnerException.GetType() == typeof(NotSupportedException),
                "The second task exception is not of the expected type."); */

            Assert.True(task1.IsFaulted && task2.IsFaulted);
            Assert.True(task1.Exception.InnerException.GetType() == typeof(InvalidOperationException));
            Assert.True(task2.Exception.InnerException.GetType() == typeof(NotSupportedException));

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
