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
        public static async Task TestRunParallelTask()
        {

            SharedEntry entry = new SharedEntry();
            await Task.Run(() =>
            {
                entry.Value = 5;
            });

            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestRunParallelSynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await Task.Run(async () =>
            {
                await Task.CompletedTask;
                entry.Value = 5;
            });

            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestRunParallelAsynchronousTask()
        {
            SharedEntry entry = new SharedEntry();
            await Task.Run(async () =>
            {
                await Task.Delay(1);
                entry.Value = 5;
            });

            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestRunNestedParallelSynchronousTask()
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

            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestAwaitNestedParallelAsynchronousTask()
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

            nekara.Api.Assert(entry.Value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }


        [Fact(Timeout = 5000)]
        public static async Task TestRunParallelSynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await Task.Run(async () =>
            {
                await Task.CompletedTask;
                entry.Value = 5;
                return entry.Value;
            });

            nekara.Api.Assert(value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestRunParallelAsynchronousTaskResult()
        {
            SharedEntry entry = new SharedEntry();
            int value = await Task.Run(async () =>
            {
                await Task.Delay(1);
                entry.Value = 5;
                return entry.Value;
            });

            nekara.Api.Assert(value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestRunNestedParallelSynchronousTaskResult()
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

            nekara.Api.Assert(value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }

        [Fact(Timeout = 5000)]
        public static async Task TestRunNestedParallelAsynchronousTaskResult()
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

            nekara.Api.Assert(value == 5, "Found unexpected value.");

            // TODO: Should be removed when session are implemented in NekaraCpp
            nekara.Api.CreateSession();
        }
    }
}
