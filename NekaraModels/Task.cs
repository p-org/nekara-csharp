using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NativeTasks = System.Threading.Tasks;
// using Nekara.Client;
using NekaraManaged.Client;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

/* Useful references:
 *   https://github.com/dotnet/roslyn/blob/master/docs/features/task-types.md
 *   https://devblogs.microsoft.com/premier-developer/extending-the-async-methods-in-c/
 *   http://blog.i3arnon.com/2016/07/25/arbitrary-async-returns/
 */

namespace Nekara.Models
{
    /// <summary>
    /// CtrlModel class
    /// </summary>
    public class CtrlModel
    {
        /// <summary>
        /// Static bool variable, which guide controlled models to send API to Nekara server or not. By default true.
        /// </summary>
        public static bool _interactwithNekara = true;
    }

    /// <summary>
    /// This is a "controlled" Task meant to replace the native System.Threading.Tasks.Task class
    /// in a user application during a test. The attribute <see cref="AsyncMethodBuilderAttribute"/> indicates that
    /// this is the Task object to be created when the <see cref="TaskMethodBuilder"/> implicitly creates
    /// an awaitable object in a async/await semantics.
    /// TODO: Check whether Task<T> inherites Task.
    /// </summary>
    [AsyncMethodBuilder(typeof(TaskMethodBuilder))]
    public class Task : IAsyncResult, IDisposable
    {
        private static NekaraManagedClient Client = RuntimeEnvironment.Client;

        public static HashSet<Task> AllPending = new HashSet<Task>();

        /// <summary>
        /// Wraps the native <see cref="System.Threading.Tasks.Task.CompletedTask"/> and returns a Task immediately.
        /// </summary>
        public static Task CompletedTask
        {
            get
            {

                if (CtrlModel._interactwithNekara)
                {
                    int taskId = Client.IdGenerator.GenerateThreadID();
                    int resourceId = Client.IdGenerator.GenerateResourceID();
                    var task = new Task(taskId, resourceId);
                    task.Completed = true;
                    task.InnerTask = NativeTasks.Task.CompletedTask;
                    return task;
                }
                else
                {
                    var task = new Task();
                    task.Completed = true;
                    task.InnerTask = NativeTasks.Task.CompletedTask;
                    return task;
                }
            }
        }

        public NativeTasks.Task InnerTask;
        public bool Completed;
        public Exception Error;

        public int TaskId { get; protected set; }
        public int ResourceId { get; protected set; }
        public int Id { get { if (TaskId == 0 && this.InnerTask != null) { return this.InnerTask.Id; } else { return TaskId; } } }
        public AggregateException Exception { get { return this.InnerTask.Exception; } }

        public NativeTasks.TaskStatus Status { get { return this.InnerTask.Status; } }

        private static readonly TaskFactory _factory = new TaskFactory();

        public static TaskFactory Factory { get { return _factory; } }


        public Task(int taskId, int resourceId)
        {
            TaskId = taskId;
            ResourceId = resourceId;

            InnerTask = null;
            Completed = false;
            Error = null;
        }

        // Constructors
        public Task(Action action)
        {
            TaskId = Client.IdGenerator.GenerateThreadID();
            ResourceId = Client.IdGenerator.GenerateResourceID();

            Client.Api.CreateResource(this.ResourceId);
            Task.AllPending.Add(this);

            InnerTask = new NativeTasks.Task(() => {
                Client.Api.CreateTask();
                Client.Api.StartTask(this.TaskId);
                action();
                Task.AllPending.Remove(this);
                Client.Api.SignalUpdatedResource(this.ResourceId);
                Client.Api.DeleteResource(this.ResourceId);
                Client.Api.EndTask(this.TaskId);
            });

            Completed = false;
            Error = null;
        }

        public Task(Action<object> action, object state)
        {
            TaskId = Client.IdGenerator.GenerateThreadID();
            ResourceId = Client.IdGenerator.GenerateResourceID();

            Client.Api.CreateResource(this.ResourceId);
            Task.AllPending.Add(this);

            InnerTask = new NativeTasks.Task(() => {
                Client.Api.CreateTask();
                Client.Api.StartTask(this.TaskId);
                action(state);
                Task.AllPending.Remove(this);
                Client.Api.SignalUpdatedResource(this.ResourceId);
                Client.Api.DeleteResource(this.ResourceId);
                Client.Api.EndTask(this.TaskId);
            });

            Completed = false;
            Error = null;

            // throw new NotImplementedException();
        }

        public Task()
        {
            if (CtrlModel._interactwithNekara)
            {
                TaskId = Client.IdGenerator.GenerateThreadID();
                ResourceId = Client.IdGenerator.GenerateResourceID();

                Client.Api.CreateResource(this.ResourceId);
            }

            InnerTask = null;

            Completed = false;
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
            if (CtrlModel._interactwithNekara)
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
            else
            {
                this.InnerTask.Wait();
            }
        }

        // Note (check): Wait(timeouts) - we don't care for timeouts
        public bool Wait(TimeSpan timeout)
        {
            if (CtrlModel._interactwithNekara)
            {
                long totalMilliseconds = (long)timeout.TotalMilliseconds;
                if (totalMilliseconds < -1 || totalMilliseconds > Int32.MaxValue)
                {
                    throw new ArgumentOutOfRangeException("timeout");
                }

                Random _r = new Random();

                // The if-then should be emitted (re-check)
                if (totalMilliseconds > 0 && (_r.Next() % 2) == 0)
                {
                    return false;
                }

                Wait();
                return true;
            }
            else
            {
                return this.InnerTask.Wait(timeout);
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

        public bool IsFaulted
        {
            get
            {
                // Faulted is "king" -- if that bit is present (regardless of other bits), we are faulted.
                return this.InnerTask.IsFaulted;
            }
        }

        public bool IsCanceled
        {
            get
            {
                // Return true if canceled bit is set and faulted bit is not set
                return this.InnerTask.IsCanceled;
            }
        }

        public static Task<TResult> FromResult<TResult>(TResult result)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();

                var _mt = new Task<TResult>(taskId, resourceId);
                _mt.InnerTask = NativeTasks.Task.FromResult(result);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
            else
            {
                var _mt = new Task<TResult>();
                _mt.InnerTask = NativeTasks.Task.FromResult(result);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
        }

        public static Task FromException(Exception exception)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();


                var _mt = new Task(taskId, resourceId);
                _mt.InnerTask = NativeTasks.Task.FromException(exception);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
            else
            {
                var _mt = new Task();
                _mt.InnerTask = NativeTasks.Task.FromException(exception);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
        }

        public static Task<TResult> FromException<TResult>(Exception exception)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();

                var _mt = new Task<TResult>(taskId, resourceId);
                _mt.InnerTask = NativeTasks.Task.FromException<TResult>(exception);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
            else
            {
                var _mt = new Task<TResult>();
                _mt.InnerTask = NativeTasks.Task.FromException<TResult>(exception);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
        }

        internal static Task FromCancellation(CancellationToken cancellationToken)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();

                var _mt = new Task(taskId, resourceId);
                _mt.InnerTask = NativeTasks.Task.FromCanceled(cancellationToken);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
            else
            {
                var _mt = new Task();
                _mt.InnerTask = NativeTasks.Task.FromCanceled(cancellationToken);
                _mt.Completed = _mt.InnerTask.IsCompleted;

                return _mt;
            }
        }

        public static Task FromCanceled(CancellationToken cancellationToken)
        {
            return Task.FromCancellation(cancellationToken);
        }

        internal static Task<TResult> FromCancellation<TResult>(CancellationToken cancellationToken)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();

                var _mt = new Task<TResult>(taskId, resourceId);
                _mt.InnerTask = NativeTasks.Task.FromCanceled<TResult>(cancellationToken);

                return _mt;
            }
            else
            {
                var _mt = new Task<TResult>();
                _mt.InnerTask = NativeTasks.Task.FromCanceled<TResult>(cancellationToken);

                return _mt;
            }
        }

        public static Task<TResult> FromCanceled<TResult>(CancellationToken cancellationToken)
        {
            return FromCancellation<TResult>(cancellationToken);
        }

        public TaskAwaiter GetAwaiter()
        {
            if (CtrlModel._interactwithNekara)
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
#if DEBUG
                    Console.WriteLine("\n[NekaraModels.Task.GetAwaiter] {0}:\t{1}", ex.GetType().Name, ex.Message);
#endif
                    this.Completed = true;
                    this.Error = ex;
                    Task.AllPending.Remove(this);
                    return new TaskAwaiter(this);
                }
            }
            else
            {
                throw new NotImplementedException();
                // return  new TaskAwaiter(this, this.InnerTask.GetAwaiter());
            }
        }

        public Task ContinueWith(Action<Task> continuation)
        {
            return ContinueWith(continuation, NativeTasks.TaskScheduler.Default);
        }

        public Task ContinueWith(Action<Task> continuation, NativeTasks.TaskScheduler scheduler)
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

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
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
#endif              
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public Task ContinueWith(Action<Task> continuation, CancellationToken cancellationToken,
                                 NativeTasks.TaskContinuationOptions continuationOptions, NativeTasks.TaskScheduler scheduler)
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);
            var t = this.InnerTask.ContinueWith((antecedent) =>
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
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
#endif              
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            }, cancellationToken, continuationOptions, scheduler);

            mt.InnerTask = t;
            return mt;
        }

        public static Task Run(Action action)
        {
            if (CtrlModel._interactwithNekara)
            {
                Task mt = _InitTask();

                var t = NativeTasks.Task.Run(() =>
                {
                    try
                    {
                        Client.Api.StartTask(mt.TaskId);
                        action();
                        mt.Completed = true;
                        Task.AllPending.Remove(mt);
                        Client.Api.SignalUpdatedResource(mt.ResourceId);
                        Client.Api.DeleteResource(mt.ResourceId);
                        Client.Api.EndTask(mt.TaskId);
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine("\n[NekaraModels.Task.Run] {0}\n    {1}", ex.GetType().Name, ex.Message);
                        /*if (ex.InnerException is TestingServiceException)
                        {
                            Console.WriteLine(ex.InnerException.StackTrace);
                        }*/
#endif
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }
                });

                mt.InnerTask = t;
                return mt;
            }
            else
            {
                var mt = new Task();
                Task.AllPending.Add(mt);

                var t = NativeTasks.Task.Run(() =>
                {
                    try
                    {
                        action();
                        mt.Completed = true;
                        Task.AllPending.Remove(mt);
                    }
                    catch (Exception ex)
                    {
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }
                });

                mt.InnerTask = t;
                return mt;
            }
        }

        // NativeTask.Run(Func<Task> function) returns a Proxy for the Task returned by the Function
        public static Task Run(Func<Task> function)
        {
            if (CtrlModel._interactwithNekara)
            {
                Task mt = _InitTask();

                var _t1 = NativeTasks.Task.Run(() =>
                {
                    try
                    {
                        Client.Api.StartTask(mt.TaskId);

                        function();

                        mt.Completed = true;

                        Task.AllPending.Remove(mt);
                        Client.Api.SignalUpdatedResource(mt.ResourceId);
                        Client.Api.DeleteResource(mt.ResourceId);
                        Client.Api.EndTask(mt.TaskId);
                    }

                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine("\n[NekaraModels.Task.Run] {0}\n    {1}", ex.GetType().Name, ex.Message);
#endif
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }

                });


                mt.InnerTask = _t1;
                return mt;
            }
            else
            {
                var mt = new Task();
                Task.AllPending.Add(mt);

                var _t1 = NativeTasks.Task.Run(() =>
                {
                    try
                    {
                        function();
                        mt.Completed = true;
                        Task.AllPending.Remove(mt);
                    }

                    catch (Exception ex)
                    {
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }

                });
                mt.InnerTask = _t1;
                return mt;
            }
        }

        // NativeTask.Run(Task<TResult> function) returns a Proxy for the Task<TResult> returned by the Function
        public static Task<TResult> Run<TResult>(Func<NativeTasks.Task<TResult>> function)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();

                var mt = new Task<TResult>(taskId, resourceId);
                Task.AllPending.Add(mt);
                Client.Api.CreateTask();
                Client.Api.CreateResource(resourceId);

                var _t1 = NativeTasks.Task.Run(() =>
                {
                    try
                    {
                        Client.Api.StartTask(taskId);

                        var _t2 = function();
                        mt.InnerTask = _t2;
                        mt.InnerTask.Wait();
                        mt.Completed = true;

                        Task.AllPending.Remove(mt);
                        Client.Api.SignalUpdatedResource(resourceId);
                        Client.Api.DeleteResource(resourceId);
                        Client.Api.EndTask(taskId);
                    }

                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine("\n[NekaraModels.Task.Run] {0}\n    {1}", ex.GetType().Name, ex.Message);
                        /*if (ex.InnerException is TestingServiceException)
                        {
                            Console.WriteLine(ex.InnerException.StackTrace);
                        }*/
#endif
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }

                });
                return mt;
            }
            else
            {
                var mt = new Task<TResult>();
                Task.AllPending.Add(mt);

                var _t1 = NativeTasks.Task.Run(() =>
                {
                    try
                    {
                        var _t2 = function();
                        mt.InnerTask = _t2;
                        mt.InnerTask.Wait();
                        mt.Completed = true;
                        Task.AllPending.Remove(mt);
                    }
                    catch (Exception ex)
                    {
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }

                });
                return mt;

            }
        }

        public static void WaitAll(params Task[] tasks)
        {
            if (CtrlModel._interactwithNekara)
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
#if DEBUG
                    Console.WriteLine($"Throwing {ignoredErrors}/{errors} Errors from Task.WaitAll!!!");
#endif
                    if (ignoredErrors > 0)
                    {
                        throw new IntentionallyIgnoredException("Multiple exceptions thrown from child Tasks", new AggregateException(inner));
                    }
                    throw new AggregateException(inner);
                }
            }
            else
            {
                NativeTasks.Task[] _tasks = new NativeTasks.Task[tasks.Length];
                for (int _i = 0; _i < tasks.Length; _i++)
                {
                    _tasks[_i] = tasks[_i].InnerTask;
                }

                NativeTasks.Task.WaitAll(_tasks);
            }
        }

        public static void WaitAny(params Task[] tasks)
        {
            if (CtrlModel._interactwithNekara)
            {
                Client.Api.ContextSwitch();
                // need to call BlockedOnAnyResource only if none of the tasks are completed already
                if (tasks.Aggregate(true, (acc, task) => acc && !task.Completed))
                {
                    Client.Api.BlockedOnAnyResource(tasks.Select(task => task.ResourceId).ToArray());
                }
            }
            else
            {
                NativeTasks.Task[] _tasks = new NativeTasks.Task[tasks.Length];
                for (int _i = 0; _i < tasks.Length; _i++)
                {
                    _tasks[_i] = tasks[_i].InnerTask;
                }

                NativeTasks.Task.WaitAny(_tasks);
            }
        }

        // CtrlModel._interactwithNekara Taken care
        public static Task WhenAll(params Task[] tasks)
        {
            return Task.Run(() => WaitAll(tasks));
        }

        //TODO: This implementation has a bug in it. Return type should be Task<Task>
        /* public static Task WhenAny(params Task[] tasks)
        {
            return Task.Run(() => WaitAny(tasks));
        } */

        public static Task<Task> WhenAny(params Task[] tasks)
        {
            if (CtrlModel._interactwithNekara)
            {
                //TODO: Replace Random number generation with Nekara random number generation

                Client.Api.ContextSwitch();
                // need to call BlockedOnAnyResource only if none of the tasks are completed already
                if (tasks.Aggregate(true, (acc, task) => acc && !task.Completed))
                {
                    Client.Api.BlockedOnAnyResource(tasks.Select(task => task.ResourceId).ToArray());
                }

                List<Task> _t3 = new List<Task>();
                foreach (Task _t1 in tasks)
                {
                    if (_t1.IsCompleted)
                        _t3.Add(_t1);
                }

                Random _rnd = new Random();

                Task<Task> _t2 = new Task<Task>(_t3[_rnd.Next(_t3.Count)]);
                _t2.Completed = true;
                return _t2;
            }
            else
            {
                NativeTasks.Task[] _tasks = new NativeTasks.Task[tasks.Length];
                for (int _i = 0; _i < tasks.Length; _i++)
                {
                    _tasks[_i] = tasks[_i].InnerTask;
                }

                NativeTasks.Task<NativeTasks.Task> _t4 = NativeTasks.Task.WhenAny(_tasks);

                Task _t5 = new Task();
                _t5.InnerTask = _t4.Result;

                Task<Task> _t2 = new Task<Task>();
                _t2.Completed = true;
                _t2.Result = _t5;
                return _t2;
            }
        }

        public static Task WhenAll(IEnumerable<Task> tasks)
        {
            Task[] taskArray = tasks as Task[];

            if (taskArray != null)
            {
                return WhenAll(taskArray);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        // TODO: Replace TaskScheduler with ControlledTaskScheduler
        public void Start()
        {
            Start(TaskScheduler.Current);
        }

        // TODO: Call Start Method with Native Task Scheduler.
        public void Start(TaskScheduler _scheduler)
        {

            try
            {
                if (_scheduler._dt == null)
                {
                    _scheduler._dt = Task.Run(() => {

                        bool _f1 = true;
                        while (true)
                        {
                            _f1 = true;
                            foreach (Task _t2 in _scheduler._taskList)
                            {
                                if (!_t2.Completed)
                                {
                                    Client.Api.ContextSwitch();
                                    _f1 = false;
                                }
                            }

                            if (_f1)
                            {
                                break;
                            }
                        }
                    });
                }
                _scheduler._taskList.Add(this);

                this.InnerTask.Start(_scheduler);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("\n\n[NekaraModels.Task.Start] rethrowing!");
                Console.WriteLine(ex);
#endif
                this.Completed = true;
                this.Error = ex;
                Task.AllPending.Remove(this);
            }
        }

        // TODO: Replace TaskScheduler with ControlledTaskScheduler
        public void Start(NativeTasks.TaskScheduler _scheduler)
        {
            try
            {
                this.InnerTask.Start(_scheduler);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("\n\n[NekaraModels.Task.Start] rethrowing!");
                Console.WriteLine(ex);
#endif
                this.Completed = true;
                this.Error = ex;
                Task.AllPending.Remove(this);
            }
        }

        public static Task Delay(TimeSpan delay)
        {
            return Delay(delay, default(CancellationToken));
        }

        public static Task Delay(int millisecondsDelay)
        {
            return Delay(millisecondsDelay, default(CancellationToken));
        }

        // TODO: Replace the fn body with cal to Delay(int, CancellationToken)
        public static Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            int totalMilliseconds = (int)delay.TotalMilliseconds;
            return Delay((int)totalMilliseconds, cancellationToken);
        }

        public static Task Delay(int millisecondsDelay, CancellationToken cancellationToken)
        {
            if (CtrlModel._interactwithNekara)
            {
                Client.Api.ContextSwitch();

                if (millisecondsDelay < -1 || millisecondsDelay > Int32.MaxValue)
                {
                    throw new ArgumentOutOfRangeException("timeout");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    // return a Task created as already-Canceled
                    return Task.FromCancellation(cancellationToken);
                }

                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();

                var _mt = new Task(taskId, resourceId);
                _mt.Completed = true;

                return _mt;
            }
            else
            {
                var _t1 = NativeTasks.Task.Delay(millisecondsDelay, cancellationToken);

                Task _t2 = new Task();
                _t2.InnerTask = _t1;
                _t2.Completed = _t1.IsCompleted;
                return _t2;
            }
        }

        public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        {
            Client.Api.ContextSwitch();
            if (this.Completed)
            {
                return new ConfiguredTaskAwaitable(this);
            }
            else
            {
                Client.Api.BlockedOnResource(this.ResourceId);
                return new ConfiguredTaskAwaitable(this);
            }

            // throw new NotImplementedException();
        }

        public struct ConfiguredTaskAwaitable
        {
            private static NekaraManagedClient _Client = RuntimeEnvironment.Client;
            public ConfiguredTaskAwaiter _t1;

            public ConfiguredTaskAwaitable(Task task)
            {
                this._t1 = new ConfiguredTaskAwaiter(task);
            }

            public ConfiguredTaskAwaiter GetAwaiter()
            {
                return this._t1;
            }

            public struct ConfiguredTaskAwaiter : ICriticalNotifyCompletion, INotifyCompletion
            {
                public Task task;
                public ConfiguredTaskAwaiter(Task task)
                {
                    this.task = task;
                }

                public bool IsCompleted { get { return true; } }

                public void GetResult()
                {
                    throw new NotImplementedException();
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


        public static YieldAwaitable Yield()
        {
            return new YieldAwaitable();
        }

        /*  public static Task<TResult> WhenAny<TResult>(params Task<TResult>[] tasks)
         {
             throw new NotImplementedException();
         } */

        public static Task<TResult[]> WhenAll<TResult>(IEnumerable<Task<TResult>> tasks)
        {
            throw new NotImplementedException();
        }

        public static Task<TResult[]> WhenAll<TResult>(params Task<TResult>[] tasks)
        {
            throw new NotImplementedException();
        }

        public static Task<Task<TResult>> WhenAny<TResult>(params Task<TResult>[] tasks)
        {
            throw new NotImplementedException();
        }

        public static Task<Task<TResult>> WhenAny<TResult>(IEnumerable<Task<TResult>> tasks)
        {
            throw new NotImplementedException();
        }

        public void Wait(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        // Internal Methods follows below
        internal static Task _InitTask()
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);

            return mt;
        }
    }


    public class TaskAwaiter : INotifyCompletion, ICriticalNotifyCompletion
    {
        public Task Task;
        internal System.Runtime.CompilerServices.TaskAwaiter _taskAwaiter;

        public TaskAwaiter(Task task)
        {
            this.Task = task;
        }

        internal TaskAwaiter(Task _task, System.Runtime.CompilerServices.TaskAwaiter _taskAwaiter)
        {
            this.Task = _task;
            this._taskAwaiter = _taskAwaiter;
        }

        public bool IsCompleted { get { if (CtrlModel._interactwithNekara) { return true; } else { var _t1 = this._taskAwaiter.IsCompleted; return _t1; } } }

        public void GetResult()
        {
            if (CtrlModel._interactwithNekara)
            {
                if (this.Task.Error != null) throw this.Task.Error;
            }
            else
            {
                this._taskAwaiter.GetResult();
            }
        }

        public void OnCompleted(Action continuation)
        {
            if (CtrlModel._interactwithNekara)
            {
                throw new NotImplementedException();
            }
            else
            {
                this._taskAwaiter.OnCompleted(continuation);
            }
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            if (CtrlModel._interactwithNekara)
            {
                throw new NotImplementedException();
            }
            else
            {
                this._taskAwaiter.UnsafeOnCompleted(continuation);
            }
        }
    }




    [AsyncMethodBuilder(typeof(TaskMethodBuilder<>))]
    public class Task<T> : Task, IAsyncResult, IDisposable
    {
        private static NekaraManagedClient Client = RuntimeEnvironment.Client;

        public new NativeTasks.Task<T> InnerTask;
        // public bool Completed;
        // public Exception Error;
        public T Result;

        // public int TaskId { get; private set; }
        // public int ResourceId { get; private set; }
        // public int Id { get { return TaskId; } }

        // public NativeTasks.TaskStatus Status { get { return this.InnerTask.Status; } }

        public Task(int taskId, int resourceId) : base(taskId, resourceId)
        {
            /* TaskId = taskId;
            ResourceId = resourceId;

            InnerTask = null;
            Completed = false;
            Error = null; */
            Result = default(T);
        }

        // Constructor
        public Task(Action action) : base(action)
        {
            /* TaskId = Client.IdGenerator.GenerateThreadID();
            ResourceId = Client.IdGenerator.GenerateResourceID();

            InnerTask = new NativeTasks.Task(action);

            Completed = false; */
            Result = default(T);
        }

        public Task() : base()
        {
            /*TaskId = Client.IdGenerator.GenerateThreadID();
            ResourceId = Client.IdGenerator.GenerateResourceID();

            Client.Api.CreateResource(this.ResourceId);

            InnerTask = null;

            Completed = false; */
            Result = default(T);
        }

        internal Task(T _p1) : base()
        {
            /*TaskId = Client.IdGenerator.GenerateThreadID();
            ResourceId = Client.IdGenerator.GenerateResourceID();

            Client.Api.CreateResource(this.ResourceId);

            InnerTask = null;

            Completed = false; */
            Result = _p1;
        }

        public new void Wait()
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
                throw ex;
            }
        }

        /* public object AsyncState => throw new NotImplementedException();

        public WaitHandle AsyncWaitHandle => throw new NotImplementedException();

        public bool CompletedSynchronously => throw new NotImplementedException();

        public bool IsCompleted { get { return this.Completed; } }

        public void Dispose()
        {
            throw new NotImplementedException();
        } */

        public new TaskAwaiter<T> GetAwaiter()
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
#if DEBUG
                Console.WriteLine("\n[NekaraModels.Task.Run] Exception in GetAwaiter, setting Error");
                Console.WriteLine("    {0}: {1}", ex.GetType().Name, ex.Message);
#endif
                this.Completed = true;
                this.Error = ex;
                return new TaskAwaiter<T>(this);
            }
        }

        public static Task<T> Run(Func<T> action)
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

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
#if DEBUG
                    Console.WriteLine("\n\n[NekaraModels.Task.Run] Exception in wrapped task, rethrowing!");
                    Console.WriteLine(ex);
#endif
                    mt.Completed = true;
                    mt.Error = ex;
                    return mt.Result;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public new ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            Client.Api.ContextSwitch();
            if (this.Completed)
            {
                return new ConfiguredTaskAwaitable<T>(this);
            }
            else
            {
                Client.Api.BlockedOnResource(this.ResourceId);
                return new ConfiguredTaskAwaitable<T>(this);
            }
        }

        public Task<TNewResult> ContinueWith<TNewResult>(Func<Task<T>, TNewResult> continuationFunction)
        {
            throw new NotImplementedException();
        }

        public Task ContinueWith(Action<Task<T>> continuationAction)
        {
            throw new NotImplementedException();
        }
    }

    public class TaskAwaiter<T> : INotifyCompletion
    {
        private static NekaraManagedClient Client = RuntimeEnvironment.Client;

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


    public struct ConfiguredTaskAwaitable<TResult>
    {
        private static NekaraManagedClient _Client = RuntimeEnvironment.Client;
        public ConfiguredTaskAwaiter _t1;

        public ConfiguredTaskAwaitable(Task<TResult> task)
        {
            this._t1 = new ConfiguredTaskAwaiter(task);
        }

        public ConfiguredTaskAwaiter GetAwaiter()
        {
            return this._t1;
        }

        public struct ConfiguredTaskAwaiter : ICriticalNotifyCompletion, INotifyCompletion
        {
            public Task<TResult> task;
            public ConfiguredTaskAwaiter(Task<TResult> task)
            {
                this.task = task;
            }

            public bool IsCompleted { get { return true; } }

            public TResult GetResult()
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


    // For the Task.Factory
    // TODO: Wrapper for TaskFactory<Tresult>
    public class TaskFactory
    {
        private NativeTasks.TaskFactory _InnerTaskFactory;
        private static readonly NekaraManagedClient Client = RuntimeEnvironment.Client;
        private TaskScheduler _taskSch;

        // Constructors
        public TaskFactory()
        {
            this._InnerTaskFactory = new NativeTasks.TaskFactory();
        }

        public TaskFactory(CancellationToken cancellationToken)
        {
            this._InnerTaskFactory = new NativeTasks.TaskFactory(cancellationToken);
        }

        public TaskFactory(NativeTasks.TaskScheduler scheduler)
        {
            this._InnerTaskFactory = new NativeTasks.TaskFactory(scheduler);
        }

        public TaskFactory(TaskScheduler scheduler)
        {
            // this._InnerTaskFactory = new NativeTasks.TaskFactory(scheduler.taskSchedulerTest);
            this._InnerTaskFactory = new NativeTasks.TaskFactory(scheduler);
            this._taskSch = scheduler;
        }

        public TaskFactory(NativeTasks.TaskCreationOptions creationOptions, NativeTasks.TaskContinuationOptions continuationOptions)
        {
            this._InnerTaskFactory = new NativeTasks.TaskFactory(creationOptions, continuationOptions);
        }

        public TaskFactory(CancellationToken cancellationToken, NativeTasks.TaskCreationOptions creationOptions, NativeTasks.TaskContinuationOptions continuationOptions, NativeTasks.TaskScheduler scheduler)
        {
            this._InnerTaskFactory = new NativeTasks.TaskFactory(creationOptions, continuationOptions);
        }

        public Task StartNew(Action action)
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);

            var t = _InnerTaskFactory.StartNew(() =>
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
#if DEBUG
                    Console.WriteLine("\n[NekaraModels.TaskFactory.StartNew] {0}\n    {1}", ex.GetType().Name, ex.Message);
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
#endif
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        // StartNew_temp() for Testing new Task Scheduler Mock();
        public Task StartNew_temp(Action action)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();


                var mt = new Task(taskId, resourceId);
                Task.AllPending.Add(mt);

                bool _f = false;

                Client.Api.CreateResource(resourceId);

                if (_taskSch._threadCount > _taskSch._taskList.Count)
                {
                    Client.Api.CreateTask();

                }
                else
                {
                    _f = true;
                }

                if (_taskSch._taskList.Count == _taskSch._threadCount)
                {
                    Task _t3 = Task.Run(() => {

                        bool _f1 = true;
                        while (true)
                        {
                            _f1 = true;
                            foreach (Task _t2 in _taskSch._taskList)
                            {
                                if (!_t2.Completed)
                                {
                                    Client.Api.ContextSwitch();
                                    _f1 = false;
                                }
                            }

                            if (_f1)
                            {
                                break;
                            }
                        }
                    });
                }

                _taskSch._taskList.Add(mt);

                var t = _InnerTaskFactory.StartNew(() =>
                {
                    try
                    {
                        if (_f)
                        {
                            Client.Api.CreateTask();
                        }
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
#if DEBUG
                        Console.WriteLine("\n[NekaraModels.TaskFactory.StartNew] {0}\n    {1}", ex.GetType().Name, ex.Message);
                        /*if (ex.InnerException is TestingServiceException)
                        {
                            Console.WriteLine(ex.InnerException.StackTrace);
                        }*/
#endif
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }
                });

                mt.InnerTask = t;
                return mt;
            }
            else
            {
                var mt = new Task();
                Task.AllPending.Add(mt);

                var t = _InnerTaskFactory.StartNew(() =>
                {
                    try
                    {
                        action();
                        mt.Completed = true;
                        Task.AllPending.Remove(mt);

                    }
                    catch (Exception ex)
                    {
                        mt.Completed = true;
                        mt.Error = ex;
                        Task.AllPending.Remove(mt);
                        return;
                    }
                });

                mt.InnerTask = t;
                return mt;
            }
        }

        public Task StartNew_temp<TResult>(Func<TResult> function)
        {
            if (CtrlModel._interactwithNekara)
            {
                int taskId = Client.IdGenerator.GenerateThreadID();
                int resourceId = Client.IdGenerator.GenerateResourceID();


                var mt = new Task(taskId, resourceId);
                Task.AllPending.Add(mt);

                bool _f = false;

                Client.Api.CreateResource(resourceId);

                if (_taskSch._threadCount > _taskSch._taskList.Count)
                {
                    Client.Api.CreateTask();

                }
                else
                {
                    _f = true;
                }

                if (_taskSch._taskList.Count == _taskSch._threadCount)
                {
                    Task _t3 = Task.Run(() => {

                        bool _f1 = true;
                        while (true)
                        {
                            _f1 = true;
                            foreach (Task _t2 in _taskSch._taskList)
                            {
                                if (!_t2.Completed)
                                {
                                    Client.Api.ContextSwitch();
                                    _f1 = false;
                                }
                            }

                            if (_f1)
                            {
                                break;
                            }
                        }
                    });
                }

                _taskSch._taskList.Add(mt);

                var t = _InnerTaskFactory.StartNew(() =>
                {

                    if (_f)
                    {
                        Client.Api.CreateTask();
                    }
                    Client.Api.StartTask(mt.TaskId);

                    var _t = function();

                    mt.Completed = true;
                    Task.AllPending.Remove(mt);
                    Client.Api.SignalUpdatedResource(mt.ResourceId);
                    Client.Api.DeleteResource(mt.ResourceId);
                    Client.Api.EndTask(mt.TaskId);
                    return _t;
                });

                mt.InnerTask = t;
                return mt;
            }
            else
            {

                var mt = new Task();
                Task.AllPending.Add(mt);

                var t = _InnerTaskFactory.StartNew(() =>
                {

                    var _t = function();
                    mt.Completed = true;
                    Task.AllPending.Remove(mt);
                    return _t;
                });

                mt.InnerTask = t;
                return mt;
            }
        }


        public Task StartNew(Action<Object> action, Object state, CancellationToken cancellationToken,
                            NativeTasks.TaskCreationOptions creationOptions, NativeTasks.TaskScheduler scheduler)
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);

            var t = _InnerTaskFactory.StartNew(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    action(state);
                    mt.Completed = true;
                    Task.AllPending.Remove(mt);
                    Client.Api.SignalUpdatedResource(resourceId);
                    Client.Api.DeleteResource(resourceId);
                    Client.Api.EndTask(taskId);
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine("\n[NekaraModels.TaskFactory.StartNew] {0}\n    {1}", ex.GetType().Name, ex.Message);
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
#endif
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            }, cancellationToken, creationOptions, scheduler);

            mt.InnerTask = t;
            return mt;
        }

        //TODO: Doubtfull
        public Task ContinueWhenAll(Task[] tasks, Action<Task[]> continuationAction)
        {
            Task.WhenAll(tasks);
            return this.StartNew(tasks, continuationAction);
        }

        //TODO: Doubtfull
        public Task ContinueWhenAny(Task[] tasks, Action<Task[]> continuationAction)
        {
            Task.WhenAny(tasks);
            return this.StartNew(tasks, continuationAction);
        }

        private Task StartNew(Task[] tasks, Action<Task[]> continuationAction)
        {
            int taskId = Client.IdGenerator.GenerateThreadID();
            int resourceId = Client.IdGenerator.GenerateResourceID();

            var mt = new Task(taskId, resourceId);
            Task.AllPending.Add(mt);
            Client.Api.CreateTask();
            Client.Api.CreateResource(resourceId);

            var t = _InnerTaskFactory.StartNew(() =>
            {
                try
                {
                    Client.Api.StartTask(taskId);
                    continuationAction(tasks);
                    mt.Completed = true;
                    Task.AllPending.Remove(mt);
                    Client.Api.SignalUpdatedResource(resourceId);
                    Client.Api.DeleteResource(resourceId);
                    Client.Api.EndTask(taskId);
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine("\n[NekaraModels.TaskFactory.StartNew] {0}\n    {1}", ex.GetType().Name, ex.Message);
                    /*if (ex.InnerException is TestingServiceException)
                    {
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }*/
#endif
                    mt.Completed = true;
                    mt.Error = ex;
                    Task.AllPending.Remove(mt);
                    return;
                }
            });

            mt.InnerTask = t;
            return mt;
        }

        public Task<TResult> StartNew<TResult>(Func<object, TResult> function, object state, CancellationToken cancellationToken, NativeTasks.TaskCreationOptions creationOptions, NativeTasks.TaskScheduler scheduler)
        {
            throw new NotImplementedException();
        }
    }


    //For Task completion source.
    public class TaskCompletionSource<TResult>
    {
        private NativeTasks.TaskCompletionSource<TResult> _tcs;
        private readonly Task<TResult> _task;
        private static readonly NekaraManagedClient _Client = RuntimeEnvironment.Client;

        // Constructors
        public TaskCompletionSource()
        {
            this._tcs = new NativeTasks.TaskCompletionSource<TResult>();
            this._task = new Task<TResult>();
            this._task.InnerTask = this._tcs.Task;
        }

        public TaskCompletionSource(object state, NativeTasks.TaskCreationOptions creationOptions)
        {
            this._tcs = new NativeTasks.TaskCompletionSource<TResult>(state, creationOptions);
            this._task = new Task<TResult>();
            this._task.InnerTask = this._tcs.Task;
        }

        public TaskCompletionSource(NativeTasks.TaskCreationOptions creationOptions)
        {
            this._tcs = new NativeTasks.TaskCompletionSource<TResult>(creationOptions);
            this._task = new Task<TResult>();
            this._task.InnerTask = this._tcs.Task;
        }


        // Methods
        public Task<TResult> Task
        {
            get { return this._task; }
        }

        public Task task
        {
            get
            {
                int taskId = _Client.IdGenerator.GenerateThreadID();
                int resourceId = _Client.IdGenerator.GenerateResourceID();

                Task _t1 = new Task(taskId, resourceId)
                {
                    InnerTask = this._tcs.Task
                };
                return _t1;
            }
        }

        public bool TrySetResult(TResult result)
        {
            bool _rval = this._tcs.TrySetResult(result);
            if (_rval)
            {
                this._task.Completed = true;
                this._task.Result = this._tcs.Task.Result;
                _Client.Api.SignalUpdatedResource(this._task.ResourceId);
                _Client.Api.DeleteResource(this._task.ResourceId);
            }
            return _rval;
        }

        public void SetResult(TResult result)
        {
            try
            {
                this._tcs.SetResult(result);
                this._task.Completed = true;
                this._task.Result = this._tcs.Task.Result;
                _Client.Api.SignalUpdatedResource(this._task.ResourceId);
                _Client.Api.DeleteResource(this._task.ResourceId);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool TrySetCanceled()
        {
            return TrySetCanceled(default(CancellationToken));
        }

        public bool TrySetCanceled(CancellationToken cancellationToken)
        {

            bool _rval = this._tcs.TrySetCanceled(cancellationToken);
            if (_rval)
            {
                this._task.Completed = true;
                this._task.Result = this._tcs.Task.Result;
                _Client.Api.SignalUpdatedResource(this._task.ResourceId);
                _Client.Api.DeleteResource(this._task.ResourceId);
            }
            return _rval;
        }

        public void SetCanceled()
        {
            try
            {
                this._tcs.SetCanceled();
                this._task.Completed = true;
                this._task.Result = this._tcs.Task.Result;
                _Client.Api.SignalUpdatedResource(this._task.ResourceId);
                _Client.Api.DeleteResource(this._task.ResourceId);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool TrySetException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException("exception");

            throw new NotImplementedException();
        }

        public bool TrySetException(IEnumerable<Exception> exceptions)
        {
            if (exceptions == null) throw new ArgumentNullException("exception");

            throw new NotImplementedException();
        }

        public bool TrySetException(IEnumerable<ExceptionDispatchInfo> exceptions)
        {
            if (exceptions == null) throw new ArgumentNullException("exception");

            throw new NotImplementedException();
        }

        public void SetException(Exception exception)
        {
            if (exception == null) throw new ArgumentNullException("exception");

            throw new NotImplementedException();
        }

        public void SetException(IEnumerable<Exception> exceptions)
        {
            if (exceptions == null) throw new ArgumentNullException("exception");

            throw new NotImplementedException();
        }
    }

    // For Task Cancelled Exception
    public class TaskCanceledException : OperationCanceledException
    {
        private NativeTasks.TaskCanceledException _tce;
        private Task _cancelledtask;

        // Constructors
        public TaskCanceledException()
        {
            this._tce = new NativeTasks.TaskCanceledException();
            this._cancelledtask = new Task();
            this._cancelledtask.InnerTask = this._tce.Task;
        }

        public TaskCanceledException(string message)
        {
            this._tce = new NativeTasks.TaskCanceledException(message);
            this._cancelledtask = new Task();
            this._cancelledtask.InnerTask = this._tce.Task;
        }

        public TaskCanceledException(string message, Exception innerException)
        {
            this._tce = new NativeTasks.TaskCanceledException(message, innerException);
            this._cancelledtask = new Task();
            this._cancelledtask.InnerTask = this._tce.Task;
        }

        public TaskCanceledException(Task task)
        {
            this._tce = new NativeTasks.TaskCanceledException(task.InnerTask);
            this._cancelledtask = task;
        }

        public Task Task
        {
            get { return _cancelledtask; }
        }
    }

    // For ValueTask
    public readonly struct ValueTask : IEquatable<ValueTask>
    {
        private readonly NativeTasks.ValueTask _valueTask;

        public ValueTask(Task task)
        {
            this._valueTask = new NativeTasks.ValueTask(task.InnerTask);
        }
        public ValueTask(NativeTasks.Sources.IValueTaskSource source, short token)
        {
            this._valueTask = new NativeTasks.ValueTask(source, token);
        }

        public bool IsCompleted { get { return this._valueTask.IsCompleted; } }
        public bool IsCompletedSuccessfully { get { return this._valueTask.IsCompletedSuccessfully; } }
        public bool IsFaulted { get { return this._valueTask.IsFaulted; } }
        public bool IsCanceled { get { return this._valueTask.IsCanceled; } }

        public Task AsTask()
        {
            throw new NotImplementedException();
        }
        public ConfiguredValueTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        {
            return this._valueTask.ConfigureAwait(continueOnCapturedContext);
        }
        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }
        public bool Equals(ValueTask other)
        {
            throw new NotImplementedException();
        }
        public ValueTaskAwaiter GetAwaiter()
        {
            return this._valueTask.GetAwaiter();
        }
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
        public ValueTask Preserve()
        {
            return this;
        }
    }


    // For ValueTask<TResult>
    public readonly struct ValueTask<TResult>
    {
        private readonly NativeTasks.ValueTask<TResult> _vt;

        public ValueTask(TResult result)
        {
            this._vt = new NativeTasks.ValueTask<TResult>(result);
        }

        public ValueTask(Task<TResult> task)
        {
            this._vt = new NativeTasks.ValueTask<TResult>(task.InnerTask);
        }

        public ValueTask(NativeTasks.Sources.IValueTaskSource<TResult> source, short token)
        {
            this._vt = new NativeTasks.ValueTask<TResult>(source, token);
        }


        public bool IsFaulted { get { return this._vt.IsFaulted; } }
        public bool IsCompletedSuccessfully { get { return this._vt.IsCompletedSuccessfully; } }
        public bool IsCompleted { get { return this._vt.IsCompleted; } }
        public bool IsCanceled { get { return this._vt.IsCanceled; } }
        public TResult Result { get { return this._vt.Result; } }


        public Task<TResult> AsTask()
        {
            throw new NotImplementedException();
        }

        public ConfiguredValueTaskAwaitable<TResult> ConfigureAwait(bool continueOnCapturedContext)
        {
            return this._vt.ConfigureAwait(continueOnCapturedContext);
        }


        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public ValueTaskAwaiter<TResult> GetAwaiter()
        {
            return this._vt.GetAwaiter();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
        public ValueTask<TResult> Preserve()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }

    }

    /* public readonly struct ValueTaskAwaiter<TResult> : ICriticalNotifyCompletion, INotifyCompletion
    {
        public bool IsCompleted { get { throw new NotImplementedException(); } }
        public TResult GetResult() { throw new NotImplementedException(); }
        public void OnCompleted(Action continuation) { throw new NotImplementedException(); }
        public void UnsafeOnCompleted(Action continuation) { throw new NotImplementedException(); }
    }

    public readonly struct ConfiguredValueTaskAwaitable<TResult>
    {
        public bool IsCompleted { get { throw new NotImplementedException(); } }
    } */


    public class SemaphoreSlim
    {
        private System.Threading.SemaphoreSlim semaphoreSlim;

        public SemaphoreSlim(int initialCount)
        {
            this.semaphoreSlim = new System.Threading.SemaphoreSlim(initialCount);
            // throw new NotImplementedException();
        }

        public Task WaitAsync()
        {
            Task _t1 = new Task();
            _t1.InnerTask = this.semaphoreSlim.WaitAsync();
            _t1.Completed = _t1.InnerTask.IsCompleted;

            return _t1;

            // throw new NotImplementedException();
        }

        public int Release()
        {
            return this.semaphoreSlim.Release();
            // throw new NotImplementedException();
        }
    }

    /* Modelling Task Scheduler.
     * Creating a test class to Inherit NativeTasks.TaskScheduler.
     * Modeled Nekara.TaskScheduler will use the test class. */

    public abstract class TaskScheduler : NativeTasks.TaskScheduler
    {
        internal List<Task> _taskList = new List<Task>();
        public int _threadCount = 64;
        internal Task _dt;

        /* protected abstract IEnumerable<Task> GetScheduledTasks(int x = 0);
        protected internal abstract void QueueTask(Task task);
        protected abstract bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued); */

        protected override IEnumerable<NativeTasks.Task> GetScheduledTasks()
        {
            throw new NotImplementedException();
        }

        protected override void QueueTask(NativeTasks.Task task)
        {
            throw new NotImplementedException();
        }

        protected override bool TryExecuteTaskInline(NativeTasks.Task task, bool taskWasPreviouslyQueued)
        {
            throw new NotImplementedException();
        }

    }


    public abstract class SingleThreadedTaskScheduler : NativeTasks.TaskScheduler
    {
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        private readonly LinkedList<System.Threading.Tasks.Task> _tasks = new LinkedList<System.Threading.Tasks.Task>(); // protected by lock(_tasks)

        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items. 
        private int _delegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism. 
        public SingleThreadedTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        // Queues a task to the scheduler. 
        protected sealed override void QueueTask(System.Threading.Tasks.Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        System.Threading.Tasks.Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        // Attempts to execute the specified task on the current thread. 
        protected sealed override bool TryExecuteTaskInline(System.Threading.Tasks.Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
                // Try to run the task. 
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler. 
        protected sealed override bool TryDequeue(System.Threading.Tasks.Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler. 
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler. 
        protected sealed override IEnumerable<System.Threading.Tasks.Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }
    }

    /* public abstract class TaskScheduler : TaskSchedulerTest
    {
        internal TaskSchedulerTest taskSchedulerTest;

        protected TaskScheduler()
        {
            taskSchedulerTest = new TaskSchedulerTest();
        }

        protected abstract IEnumerable<Task> GetScheduledTasks();
        protected internal abstract void QueueTask(Task task);
        protected abstract bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued);


        public static NativeTasks.TaskScheduler Current { get { return TaskSchedulerTest.Current; } }

        public static NativeTasks.TaskScheduler Default { get { return TaskSchedulerTest.Default; } }

        public int Id => taskSchedulerTest.Id;

        public virtual int MaximumConcurrencyLevel => taskSchedulerTest.MaximumConcurrencyLevel;

        
        public static event EventHandler<NativeTasks.UnobservedTaskExceptionEventArgs> UnobservedTaskException;

        
        public static TaskScheduler FromCurrentSynchronizationContext()
        {
            throw new NotImplementedException();
        }

        protected bool TryExecuteTask(Task task)
        {
            return taskSchedulerTest.TryExecuteTask_dummy(task.InnerTask);
        }

        protected internal virtual bool TryDequeue(Task task)
        {
           return false;
        }
    } */

    /* public abstract class TaskScheduler  // : NativeTasks.TaskScheduler
    {
        private TaskSchedulerTest taskSchedulerTest;

        protected TaskScheduler()
        {
            throw new NotImplementedException();
        }


        public static TaskScheduler Current { get { throw new NotImplementedException(); } }

        public static TaskScheduler Default { get { throw new NotImplementedException(); } }

        public int Id { get { throw new NotImplementedException(); } }

        public virtual int MaximumConcurrencyLevel { get; }


        public static event EventHandler<NativeTasks.UnobservedTaskExceptionEventArgs> UnobservedTaskException;


        public static TaskScheduler FromCurrentSynchronizationContext()
        {
            throw new NotImplementedException();
        }

        protected virtual IEnumerable<Task> GetScheduledTasks()
        {
            throw new NotImplementedException();
        }

        // protected IEnumerable<NativeTasks.Task> GetScheduledTasks()
        // {
            // throw new NotImplementedException();
        // } 

        protected bool TryExecuteTask(Task task)
        {
            throw new NotImplementedException();
        }

        protected abstract bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued);

        protected internal abstract void QueueTask(Task task);

        protected bool TryExecuteTaskInline(NativeTasks.Task task, bool taskWasPreviouslyQueued)
        {
            throw new NotImplementedException();
        }

        protected void QueueTask(NativeTasks.Task task)
        {
            throw new NotImplementedException();
        }

        protected internal virtual bool TryDequeue(Task task)
        {
            throw new NotImplementedException();
        }

    } */

    namespace Xunit
    {
        public interface IAsyncLifetime
        {
            Task DisposeAsync();
            Task InitializeAsync();
        }
    }

}
