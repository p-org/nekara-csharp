using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using System.Reflection;

/* Useful references:
 *   https://github.com/dotnet/roslyn/blob/master/docs/features/task-types.md
 *   https://devblogs.microsoft.com/premier-developer/extending-the-async-methods-in-c/
 *   http://blog.i3arnon.com/2016/07/25/arbitrary-async-returns/
 */

namespace Nekara.Models
{
    [AsyncMethodBuilder(typeof(TaskMethodBuilder))]
    public class Task : IAsyncResult, IDisposable
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public NativeTasks.Task InnerTask;
        public bool Completed;
        public Exception Error;

        public int TaskId { get; private set; }
        public int ResourceId { get; private set; }
        public int Id { get { return TaskId; } }

        public Task(int taskId, int resourceId)
        {
            TaskId = taskId;
            ResourceId = resourceId;

            InnerTask = null;
            Completed = false;
            Error = null;
        }

        public void Wait()
        {
            try
            {
                Client.Api.ContextSwitch();
                if (this.Completed)
                {
                    return;
                }
                else
                {
                    Client.Api.BlockedOnResource(this.ResourceId);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[NekaraModels.Task.Run] Exception in Nekara.Models.Task.Wait, rethrowing Error");
                Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                this.Completed = true;
                throw ex;
            }
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
                    Client.Api.BlockedOnResource(this.ResourceId);
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
            int taskId = Client.TaskIdGenerator.Generate();
            int resourceId = Client.ResourceIdGenerator.Generate();

            var mt = new Task(taskId, resourceId);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);
            var t = NativeTasks.Task.Run(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    action();
                    mt.Completed = true;
                    Client.Api.SignalUpdatedResource(resourceId);
                    Client.Api.DeleteResource(resourceId);
                    Client.Api.EndTask(taskId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\n[NekaraModels.Task.Run] Exception in wrapped task, setting Error");
                    Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                    if (ex is TargetInvocationException || ex is AggregateException)
                    {
                        Console.WriteLine(ex.InnerException);
                    }
                    //Console.WriteLine(ex);
                    mt.Completed = true;
                    mt.Error = ex;
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public static void WaitAll(params Task[] tasks)
        {
            Client.Api.ContextSwitch();
            // we can simply sequentially wait for all the tasks
            foreach (Task task in tasks)
            {
                if (!task.Completed) Client.Api.BlockedOnResource(task.ResourceId);
            }
        }

        public static void WaitAny(params Task[] tasks)
        {
            Client.Api.ContextSwitch();
            // need to call BlockedOnAnyResource only if none of the tasks are completed already
            if (tasks.Aggregate(true, (acc, task)=> acc && !task.Completed))
            {
                Client.Api.BlockedOnAnyResource(tasks.Select(task => task.ResourceId).ToArray());
            }
        }

        public static Task WhenAll(params Task[] tasks)
        {
            return Task.Run(() => WaitAll(tasks));
            //return NativeTasks.Task.WhenAll(tasks.Select(t => t.InnerTask).ToArray());
        }

        public static Task WhenAny(params Task[] tasks)
        {
            return Task.Run(() => WaitAny(tasks));
        }
    }

    public class TaskAwaiter : INotifyCompletion, ICriticalNotifyCompletion
    {
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


    [AsyncMethodBuilder(typeof(TaskMethodBuilder<>))]
    public class Task<T> : IAsyncResult, IDisposable
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public NativeTasks.Task InnerTask;
        public bool Completed;
        public Exception Error;
        public T Result;

        public int TaskId { get; private set; }
        public int ResourceId { get; private set; }
        public int Id { get { return TaskId; } }

        public Task(int taskId, int resourceId)
        {
            TaskId = taskId;
            ResourceId = resourceId;

            InnerTask = null;
            Completed = false;
            Error = null;
            Result = default(T);
        }

        public void Wait()
        {
            try
            {
                Client.Api.ContextSwitch();
                if (this.Completed)
                {
                    return;
                }
                else
                {
                    Client.Api.BlockedOnResource(this.ResourceId);
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[NekaraModels.Task.Run] Exception in Nekara.Models.Task.Wait, rethrowing Error");
                Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                this.Completed = true;
                throw ex;
            }
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
            try
            {
                Client.Api.ContextSwitch();
                if (this.Completed)
                {
                    return new TaskAwaiter<T>(this);
                }
                else
                {
                    Client.Api.BlockedOnResource(this.ResourceId);
                    return new TaskAwaiter<T>(this);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[NekaraModels.Task.Run] Exception in GetAwaiter, setting Error");
                Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
                this.Completed = true;
                this.Error = ex;
                return new TaskAwaiter<T>(this);
            }
        }

        public static Task<T> Run(Func<T> action)
        {
            int taskId = Client.TaskIdGenerator.Generate();
            int resourceId = Client.ResourceIdGenerator.Generate();

            var mt = new Task<T>(taskId, resourceId);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);
            var t = NativeTasks.Task<T>.Run<T>(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    mt.Result = action();
                    mt.Completed = true;
                    Client.Api.SignalUpdatedResource(resourceId);
                    Client.Api.DeleteResource(resourceId);
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
