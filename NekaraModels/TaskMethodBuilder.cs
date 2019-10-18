using System;
using NativeTasks = System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Nekara.Client;

namespace Nekara.Models
{
    public sealed class TaskMethodBuilder
    {
        private static NekaraClient nekara = RuntimeEnvironment.Client;

        private readonly Task _Task;

        public TaskMethodBuilder() {
            int taskId = nekara.IdGen.Generate();
            this._Task = new Task(taskId);
            nekara.Api.CreateTask();
            nekara.Api.CreateResource(taskId);
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
            nekara.Api.SignalUpdatedResource(this._Task.Id);
            nekara.Api.EndTask(this._Task.Id);
        }
        public void SetException(Exception exception)
        {
            this._Task.Completed = true;
            this._Task.Error = exception;
        }
    }
}