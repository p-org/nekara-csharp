using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;
using NekaraUnitTest.Common;

namespace NekaraUnitTest
{
    public class TaskExceptionTests
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
        public async Task TestNoSynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = WriteAsync(entry, 5);
            await task;

            nekara.Api.WaitForMainTask();

            // nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, "Found unexpected task status.");
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion);

            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestNoAsynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = WriteWithDelayAsync(entry, 5);
            await task;

            nekara.Api.WaitForMainTask();

            // nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, "Found unexpected task status.");
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestNoParallelSynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = Task.Run(() =>
            {
                entry.Value = 5;
            });

            await task;

            nekara.Api.WaitForMainTask();

            // nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, "Found unexpected task status.");
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestNoParallelAsynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = Task.Run(async () =>
            {
                entry.Value = 5;
                await Task.Delay(1);
            });
            await task;

            nekara.Api.WaitForMainTask();

            // nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, "Found unexpected task status.");
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestNoParallelFuncTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            async Task func()
            {
                entry.Value = 5;
                await Task.Delay(1);
            }

            var task = Task.Run(func);
            await task;

            nekara.Api.WaitForMainTask();

            // nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, "Found unexpected task status.");
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        private async Task WriteWithExceptionAsync(SharedEntry entry, int value)
        {
            await Task.CompletedTask;
            entry.Value = value;
            throw new InvalidOperationException();
        }

        private async Task WriteWithDelayedExceptionAsync(SharedEntry entry, int value)
        {
            await Task.Delay(1);
            entry.Value = value;
            throw new InvalidOperationException();
        }

        [Fact(Timeout = 5000)]
        public async Task TestSynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = WriteWithExceptionAsync(entry, 5);

            Exception exception = null;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            nekara.Api.WaitForMainTask();


            // nekara.Api.Assert(exception.GetType() == typeof(InvalidOperationException),
               //  "The exception is not of the expected type.");
            // nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.Faulted, "Found unexpected task status.");
            Assert.True(exception.GetType() == typeof(InvalidOperationException));
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.Faulted);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestAsynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = WriteWithDelayedExceptionAsync(entry, 5);

            Exception exception = null;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            nekara.Api.WaitForMainTask();

            /* nekara.Api.Assert(exception.GetType() == typeof(InvalidOperationException),
                "The exception is not of the expected type.");
            nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.Faulted, "Found unexpected task status."); */
            Assert.True(exception.GetType() == typeof(InvalidOperationException));
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.Faulted);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestParallelSynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = Task.Run(() =>
            {
                entry.Value = 5;
                throw new InvalidOperationException();
            });

            Exception exception = null;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            nekara.Api.WaitForMainTask();

            /* nekara.Api.Assert(exception.GetType() == typeof(InvalidOperationException),
                "The exception is not of the expected type.");
            nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.Faulted, "Found unexpected task status."); */
            Assert.True(exception.GetType() == typeof(InvalidOperationException));
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.Faulted);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestParallelAsynchronousTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            var task = Task.Run(async () =>
            {
                entry.Value = 5;
                await Task.Delay(1);
                throw new InvalidOperationException();
            });

            Exception exception = null;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            nekara.Api.WaitForMainTask();

            /* nekara.Api.Assert(exception.GetType() == typeof(InvalidOperationException),
                "The exception is not of the expected type.");
            nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.Faulted, "Found unexpected task status."); */
            Assert.True(exception.GetType() == typeof(InvalidOperationException));
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.Faulted);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public async Task TestParallelFuncTaskExceptionStatus()
        {
            SharedEntry entry = new SharedEntry();
            async Task func()
            {
                entry.Value = 5;
                await Task.Delay(1);
                throw new InvalidOperationException();
            }

            var task = Task.Run(func);

            Exception exception = null;
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            nekara.Api.WaitForMainTask();

            /* nekara.Api.Assert(exception.GetType() == typeof(InvalidOperationException),
                "The exception is not of the expected type.");
            nekara.Api.Assert(task.Status == System.Threading.Tasks.TaskStatus.Faulted, "Found unexpected task status."); */
            Assert.True(exception.GetType() == typeof(InvalidOperationException));
            Assert.True(task.Status == System.Threading.Tasks.TaskStatus.Faulted);
            // nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");
            Assert.True(entry.Value == 5);

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
