using System;
using NativeTasks = System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Nekara.Client;

/* Useful references:
 *   https://github.com/dotnet/roslyn/blob/master/docs/features/task-types.md
 *   https://devblogs.microsoft.com/premier-developer/extending-the-async-methods-in-c/
 *   http://blog.i3arnon.com/2016/07/25/arbitrary-async-returns/
 */

namespace Nekara.Models
{
    public sealed class TaskMethodBuilder
    {
        private static NekaraClient nekara = RuntimeEnvironment.Client;

        private readonly Task _Task;

        public TaskMethodBuilder() {
            int taskId = nekara.TaskIdGenerator.Generate();
            int resourceId = nekara.ResourceIdGenerator.Generate();

            this._Task = new Task(taskId, resourceId);
            Task.AllPending.Add(this._Task);
            nekara.Api.CreateTask();
            nekara.Api.CreateResource(resourceId);
        }

        public static TaskMethodBuilder Create() => new TaskMethodBuilder();
        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            var sm = stateMachine;
            NativeTasks.Task.Run(() => nekara.Api.StartTask(this._Task.Id))
                .ContinueWith(prev => sm.MoveNext());
        }

        public Task Task { get { return this._Task; } }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }
        public void SetResult()
        {
            this._Task.Completed = true;
            Task.AllPending.Remove(this._Task);
            nekara.Api.SignalUpdatedResource(this._Task.ResourceId);
            nekara.Api.DeleteResource(this._Task.ResourceId);
            nekara.Api.EndTask(this._Task.Id);
        }
        public void SetException(Exception exception)
        {
            Console.WriteLine("<<< {0} in Custom Await >>>", exception.GetType().Name);
            this._Task.Completed = true;
            this._Task.Error = exception;
            Task.AllPending.Remove(this._Task);
        }
    }


    public sealed class TaskMethodBuilder<TResult>
    {
        private static NekaraClient nekara = RuntimeEnvironment.Client;

        private readonly Task<TResult> _Task;

        public TaskMethodBuilder()
        {
            int taskId = nekara.TaskIdGenerator.Generate();
            int resourceId = nekara.ResourceIdGenerator.Generate();

            this._Task = new Task<TResult>(taskId, resourceId);
            nekara.Api.CreateTask();
            nekara.Api.CreateResource(resourceId);
        }

        public static TaskMethodBuilder<TResult> Create() => new TaskMethodBuilder<TResult>();
        public void SetStateMachine(IAsyncStateMachine stateMachine) { }

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
        {
            var sm = stateMachine;
            NativeTasks.Task<TResult>.Run(() => nekara.Api.StartTask(this._Task.Id))
                .ContinueWith(prev => sm.MoveNext());
        }

        public Task<TResult> Task { get { return this._Task; } }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : INotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }
        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
            ref TAwaiter awaiter,
            ref TStateMachine stateMachine)
            where TAwaiter : ICriticalNotifyCompletion
            where TStateMachine : IAsyncStateMachine
        {
            stateMachine.MoveNext();
        }
        public void SetResult(TResult result)
        {
            this._Task.Result = result;
            this._Task.Completed = true;
            nekara.Api.SignalUpdatedResource(this._Task.ResourceId);
            nekara.Api.DeleteResource(this._Task.ResourceId);
            nekara.Api.EndTask(this._Task.Id);
        }
        public void SetException(Exception exception)
        {
            this._Task.Completed = true;
            this._Task.Error = exception;
        }
    }
}