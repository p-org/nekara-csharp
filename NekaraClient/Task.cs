using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NativeTasks = System.Threading.Tasks;
using Nekara.Client;
using System.Reflection;
using System.Collections.Generic;

/* Useful references:
 *   https://github.com/dotnet/roslyn/blob/master/docs/features/task-types.md
 *   https://devblogs.microsoft.com/premier-developer/extending-the-async-methods-in-c/
 *   http://blog.i3arnon.com/2016/07/25/arbitrary-async-returns/
 */

namespace Nekara.Models
{
    /// <summary>
    /// This is a "controlled" Task meant to replace the native System.Threading.Tasks.Task class
    /// in a user application during a test. The attribute <see cref="AsyncMethodBuilderAttribute"/> indicates that
    /// this is the Task object to be created when the <see cref="TaskMethodBuilder"/> implicitly creates
    /// an awaitable object in a async/await semantics.
    /// </summary>
    [AsyncMethodBuilder(typeof(TaskMethodBuilder))]
    public class Task : IAsyncResult, IDisposable
    {
        private static NekaraClient Client = RuntimeEnvironment.Client;

        public static HashSet<Task> AllPending = new HashSet<Task>();

        /// <summary>
        /// Wraps the native <see cref="System.Threading.Tasks.Task.CompletedTask"/> and returns a Task immediately.
        /// </summary>
        public static Task CompletedTask
        {
            get
            {
                int taskId = Client.TaskIdGenerator.Generate();
                int resourceId = Client.ResourceIdGenerator.Generate();
                var task = new Task(taskId, resourceId);
                task.Completed = true;
                task.InnerTask = NativeTasks.Task.CompletedTask;
                return task;
            }
        }

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

        /// <summary>
        /// Synchronously wait for the Task to complete (same interface as <see cref="System.Threading.Tasks.Task.Wait()"/>).
        /// When invoked, it will first call <see cref="TestRuntimeApi.ContextSwitch()"/> to yield control to the scheduler (server)
        /// and give a chance for some Task(s) to execute. After receiving control back, it will first see if the Task has
        /// completed, returning immediately if it has already completed. If not, it will call <see cref="TestRuntimeApi.BlockedOnResource(int)"/>
        /// with the Task's <see cref="ResourceId"/> to block execution until the Task is completed
        /// (i.e., until the corresponding <see cref="ResourceId"/> has been signalled as updated).
        /// </summary>
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
#if DEBUG
                Console.WriteLine("\n[NekaraModels.Task.Run] Exception in Nekara.Models.Task.Wait, rethrowing Error");
                Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
#endif
                this.Completed = true;
                Task.AllPending.Remove(this);
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
#if !DEBUG
                Console.WriteLine("\n[NekaraModels.Task.GetAwaiter] {0}:\t{1}", ex.GetType().Name, ex.Message);
#endif
                this.Completed = true;
                this.Error = ex;
                Task.AllPending.Remove(this);
                return new TaskAwaiter(this);
            }
        }

        public Task ContinueWith(Action<Task> continuation)
        {
            int taskId = Client.TaskIdGenerator.Generate();
            int resourceId = Client.ResourceIdGenerator.Generate();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);
            var t = NativeTasks.Task.Run(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    if (!this.IsCompleted) Client.Api.BlockedOnResource(this.ResourceId);
                    continuation(this);
                    mt.Completed = true;
                    Task.AllPending.Remove(mt);
                    Client.Api.SignalUpdatedResource(resourceId);
                    Client.Api.DeleteResource(resourceId);
                    Client.Api.EndTask(taskId);
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine("\n[NekaraModels.Task.Run] {0} in wrapped task, setting Error\n\t{1}", ex.GetType().Name, ex.Message);
#endif
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public static Task Run(Action action)
        {
            int taskId = Client.TaskIdGenerator.Generate();
            int resourceId = Client.ResourceIdGenerator.Generate();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);
            var t = NativeTasks.Task.Run(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    action();
                    mt.Completed = true;
                    Task.AllPending.Remove(mt);
                    Client.Api.SignalUpdatedResource(resourceId);
                    Client.Api.DeleteResource(resourceId);
                    Client.Api.EndTask(taskId);
                }
                catch (Exception ex)
                {
#if !DEBUG
                    Console.WriteLine("\n[NekaraModels.Task.Run] {0}\n    {1}", ex.GetType().Name, ex.Message);
#endif
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public static void WaitAll(params Task[] tasks)
        {
            // we could call task.Wait on each task, but doing it this way to avoid
            // calling ContextSwitch multiple times
            Client.Api.ContextSwitch();

            int errors = 0;
            int ignoredErrors = 0;
            
            // we can simply sequentially wait for all the tasks
            foreach (Task task in tasks)
            {
                try
                {
                    if (!task.Completed) Client.Api.BlockedOnResource(task.ResourceId);
                }
                catch (Exception ex)
                {
                    task.Completed = true;
                    task.Error = ex;
                    Task.AllPending.Remove(task);
                    errors++;
                    if (ex is IntentionallyIgnoredException) ignoredErrors++;
                }
            }

            if (errors > 0)
            {
                var inner = tasks.Where(task => task.Error != null).Select(task => task.Error).ToArray();
                Console.WriteLine($"Throwing {ignoredErrors}/{errors} Errors from Task.WaitAll!!!");
                if (ignoredErrors > 0)
                {
                    throw new IntentionallyIgnoredException("Multiple exceptions thrown from child Tasks", new AggregateException(inner));
                }
                throw new AggregateException(inner);
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
            /*return Task.Run(() => {
                try
                {
                    WaitAll(tasks);
                }
                // we need to catch any internal exceptions here and silence it,
                // otherwise this Task itself will also throw an error
                // and the main thread will not be able to catch all the exceptions
                catch (IntentionallyIgnoredException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                // rethrow the error if it was not expected
                catch (Exception ex)
                {
                    throw;
                }
            });*/
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
