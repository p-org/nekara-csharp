using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;

namespace Nekara.Models
{
    public class Task : IAsyncResult, IDisposable
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public int Id;
        public NativeTasks.Task InnerTask;
        public bool Completed;
        public Exception Error;

        public Task(int id)
        {
            Id = id;
            InnerTask = null;
            Completed = false;
            Error = null;
        }

        public object AsyncState => throw new NotImplementedException();

        public WaitHandle AsyncWaitHandle => throw new NotImplementedException();

        public bool CompletedSynchronously => throw new NotImplementedException();

        public bool IsCompleted { get { return this.Completed; } }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public TaskAwaiter GetAwaiter()
        {
            try
            {
                Client.Api.ContextSwitch();
                if (this.Completed)
                {
                    return new TaskAwaiter(this);
                }
                else
                {
                    Client.Api.BlockedOnResource(this.Id);
                    return new TaskAwaiter(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[NekaraModels.Task.Run] Exception in GetAwaiter, setting Error");
                Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                this.Completed = true;
                this.Error = ex;
                return new TaskAwaiter(this);
            }
        }

        public static Task Run(Action action)
        {
            int taskId = Client.IdGen.Generate();
            var mt = new Task(taskId);
            Client.Api.CreateTask();
            Client.Api.CreateResource(taskId);
            var t = NativeTasks.Task.Run(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    action();
                    mt.Completed = true;
                    Client.Api.SignalUpdatedResource(taskId);
                    Client.Api.EndTask(taskId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\n[NekaraModels.Task.Run] Exception in wrapped task, setting Error");
                    Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                    //Console.WriteLine(ex);
                    mt.Completed = true;
                    mt.Error = ex;
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public static NativeTasks.Task WhenAll(params Task[] tasks)
        {
            return NativeTasks.Task.WhenAll(tasks.Select(t => t.InnerTask).ToArray());
        }
    }

    public class TaskAwaiter : INotifyCompletion
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public Task Task;

        public TaskAwaiter(Task task)
        {
            this.Task = task;
        }
        
        public bool IsCompleted { get { return true; } }

        public void GetResult()
        {
            if (this.Task.Error != null) throw this.Task.Error;
        }
        
        public void OnCompleted(Action continuation)
        {
            throw new NotImplementedException();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            throw new NotImplementedException();
        }
    }


    public class Task<T> : IAsyncResult, IDisposable
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public int Id;
        public NativeTasks.Task InnerTask;
        public bool Completed;
        public Exception Error;
        public T Result;

        public Task(int id)
        {
            Id = id;
            InnerTask = null;
            Completed = false;
            Error = null;
            Result = default(T);
        }

        public object AsyncState => throw new NotImplementedException();

        public WaitHandle AsyncWaitHandle => throw new NotImplementedException();

        public bool CompletedSynchronously => throw new NotImplementedException();

        public bool IsCompleted { get { return this.Completed; } }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public TaskAwaiter<T> GetAwaiter()
        {
            Client.Api.ContextSwitch();
            if (this.Completed)
            {
                return new TaskAwaiter<T>(this);
            }
            else
            {
                Client.Api.BlockedOnResource(this.Id);
                return new TaskAwaiter<T>(this);
            }
        }

        public static Task<T> Run(Func<T> action)
        {
            int taskId = Client.IdGen.Generate();
            var mt = new Task<T>(taskId);
            Client.Api.CreateTask();
            Client.Api.CreateResource(taskId);
            var t = NativeTasks.Task<T>.Run<T>(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    mt.Result = action();
                    mt.Completed = true;
                    Client.Api.SignalUpdatedResource(taskId);
                    Client.Api.EndTask(taskId);
                    return mt.Result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\n\n[NekaraModels.Task.Run] Exception in wrapped task, rethrowing!");
                    Console.WriteLine(ex);
                    mt.Completed = true;
                    mt.Error = ex;
                    return mt.Result;
                }
            });

            mt.InnerTask = t;
            return mt;
        }
    }

    public class TaskAwaiter<T> : INotifyCompletion
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public Task<T> task;

        public TaskAwaiter(Task<T> task)
        {
            this.task = task;
        }

        public bool IsCompleted { get { return true; } }

        public T GetResult()
        {
            if (this.task.Error != null) throw this.task.Error;
            return this.task.Result;
        }

        public void OnCompleted(Action continuation)
        {
            throw new NotImplementedException();
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            throw new NotImplementedException();
        }
    }
}
