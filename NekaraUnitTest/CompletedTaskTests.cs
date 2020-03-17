using System;
using System.Threading;
using Xunit;
using NekaraManaged.Client;
using Nekara.Models;

namespace NekaraUnitTest
{
    public class CompletedTaskTests
    {

        [Fact(Timeout = 5000)]
        public static void TestCompletedTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task task = Task.CompletedTask;
            nekara.Api.Assert(task.IsCompleted, "The task has not completed.");
        }

        [Fact(Timeout = 5000)]
        public static void TestCanceledTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            CancellationToken token = new CancellationToken(true);
            Task task = Task.FromCanceled(token);
            nekara.Api.Assert(task.IsCanceled, "The task is not cancelled.");
        }

        [Fact(Timeout = 5000)]
        public static void TestCanceledTaskWithResult()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            CancellationToken token = new CancellationToken(true);
            Task<int> task = Task.FromCanceled<int>(token);
            nekara.Api.Assert(task.IsCanceled, "The task is not cancelled.");
        }

        [Fact(Timeout = 5000)]
        public static void TestFailedTask()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task task = Task.FromException(new InvalidOperationException());
            nekara.Api.Assert(task.IsFaulted, "The task is not faulted.");
            nekara.Api.Assert(task.Exception.GetType() == typeof(AggregateException), "The exception is not of the expected type.");
            nekara.Api.Assert(task.Exception.InnerException.GetType() == typeof(InvalidOperationException), "The exception is not of the expected type.");
        }

        [Fact(Timeout = 5000)]
        public static void TestFailedTaskWithResult()
        {
            NekaraManagedClient nekara = RuntimeEnvironment.Client;
            nekara.Api.CreateSession();

            Task<int> task = Task.FromException<int>(new InvalidOperationException());
            nekara.Api.Assert(task.IsFaulted, "The task is not faulted.");
            nekara.Api.Assert(task.Exception.GetType() == typeof(AggregateException), "The exception is not of the expected type.");
            nekara.Api.Assert(task.Exception.InnerException.GetType() == typeof(InvalidOperationException), "The exception is not of the expected type.");
        }

    }
}
