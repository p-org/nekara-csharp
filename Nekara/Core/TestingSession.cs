using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nekara.Abstractions;

namespace Nekara.Core
{
    /// <summary>
    /// This object represents a single run of a user program under test.
    /// It maintains a <see cref="ProgramState"/> object and exposes the
    /// testing service API defined in <see cref="ITestingService"/>.
    /// The core testing logic is implemented by this object.
    /// </summary>
    public class TestingSession : ITestingService
    {
        // It appears that Process.GetCurrentProcess is a very expensive call
        // (makes the whole app 10 ~ 15x slower when called in AppendLog), so we save the reference here.
        // However, this object is used only for debugging and can be omitted entirely.
        private static Process currentProcess = Process.GetCurrentProcess();
        private static int PrintVerbosity = 0;

        public static Helpers.MicroProfiler Profiler = new Helpers.MicroProfiler();

        private static Helpers.UniqueIdGenerator IdGen = new Helpers.UniqueIdGenerator(true, 1);

        // metadata
        public SessionInfo Meta;

        // run-time objects
        private readonly StreamWriter traceFile;
        private readonly StreamWriter logFile;
        public event EventHandler<SessionRecord> OnComplete;
        private readonly object stateLock;
        private Timer timeout;
        private Concurrent<bool> IsFinished;

        // testing service objects
        private Helpers.SeededRandomizer randomizer;
        private int counter;
        ProgramState programState;
        TaskCompletionSource<SessionRecord> SessionFinished;
        HashSet<RemoteMethodInvocation> pendingRequests;    // to keep track of unresolved requests. This can actually be done with a simple counter, but we want to present useful information to the client.
        List<DecisionTrace> testTrace;
        List<string> runtimeLog;

        // result data
        public List<SessionRecord> records;
        public SessionRecord currentRecord;

        public TestingSession(string assemblyName, string assemblyPath, string methodDeclaringClass, string methodName, int schedulingSeed, int timeoutMs = Constants.SessionTimeoutMs, int maxDecisions = Constants.SessionMaxDecisions)
        {
            this.Meta = new SessionInfo(IdGen.Generate().ToString(), assemblyName, assemblyPath, methodDeclaringClass, methodName, schedulingSeed, timeoutMs, maxDecisions);

            this.records = new List<SessionRecord>();

            // initialize run-time objects
            // this._onComplete = record => { };     // empty onComplete handler
            this.stateLock = new object();
            this.traceFile = File.AppendText(this.traceFilePath);
            this.logFile = File.AppendText(this.logFilePath);
            this.IsFinished = new Concurrent<bool>();

            this.Reset();
        }

        public TestingSession(SessionInfo meta) :
            this(meta.assemblyName,
                meta.assemblyPath,
                meta.methodDeclaringClass,
                meta.methodName,
                meta.schedulingSeed,
                meta.timeoutMs,
                meta.maxDecisions) { }

        public string Id { get { return this.Meta.id; } }

        public string traceFilePath { get { return "logs/run-" + NekaraServer.StartedAt.ToString() + "-" + this.Id + "-trace.csv"; } }

        public string logFilePath { get { return "logs/run-" + NekaraServer.StartedAt.ToString() + "-" + this.Id + "-log.csv"; } }

        // public bool IsFinished { get; set; }

        public bool IsReplayMode { get { return this.records.Count > 0; } }

        public SessionRecord LastRecord { get { return this.records.Last(); } }

        /// <summary>
        /// Used internally to add a record of the scheduling decision made.
        /// The list of decisions are used to compare with any future runs using the same seed,
        /// in order to check that the run is reproduced.
        /// </summary>
        /// <param name="decisionType">Either ContextSwitch, CreateNondetBool, or CreateNondetInteger</param>
        /// <param name="decisionValue">Either the selected Task ID, the boolean value, or the integer value generated</param>
        /// <param name="currentTask">The Task ID of the current Task when the decision was made</param>
        /// <param name="state">The <see cref="ProgramState"/> when the decision was made</param>
        private void PushTrace(DecisionType decisionType, int decisionValue, int currentTask, ProgramState state)
        {
            lock (this.testTrace)
            {
                var decision = new DecisionTrace(decisionType, decisionValue, currentTask, state.GetAllTasksTuple());
                // Console.WriteLine(decision.ToReadableString());
                this.testTrace.Add(decision);
            }
        }

        private void AppendLog(params object[] cols)
        {
            lock (this.runtimeLog)
            {
                if (PrintVerbosity > 1) Console.WriteLine(String.Join("\t", cols.Select(obj => obj.ToString())));
                this.runtimeLog.Add(String.Join("\t", cols.Select(obj => obj.ToString())));
            }
        }

        /// <summary>
        /// Prints the record of decisions made to the console. Used mainly for debugging purposes.
        /// </summary>
        private void PrintTrace(List<DecisionTrace> list)
        {
            list.ForEach(trace => Console.WriteLine(trace.ToReadableString()));
        }

        /// <summary>
        /// Called before starting a new test run to clear all the transient values to its initial values.
        /// </summary>
        public void Reset()
        {
            lock (this.stateLock)
            {
                // reset run-time objects
                this.randomizer = new Helpers.SeededRandomizer(this.Meta.schedulingSeed);
                this.counter = 0;
                this.programState = new ProgramState();
                this.SessionFinished = new TaskCompletionSource<SessionRecord>();
                this.pendingRequests = new HashSet<RemoteMethodInvocation>();
                this.testTrace = new List<DecisionTrace>();
                this.runtimeLog = new List<string>();
                this.IsFinished.Value = false;
                this.timeout = new Timer(_ =>
                {
                    string currentTask = this.programState.currentTask.ToString();
                    var errorInfo = $"No activity for {(Meta.timeoutMs / 1000).ToString()} seconds!\n  Program State: [ {this.programState.GetCurrentStateString()} ]\n  Possible reasons:\n\t- Not calling EndTask({currentTask})\n\t- Calling ContextSwitch from an undeclared Task\n\t- Some Tasks not being modelled";
                    this.Finish(TestResult.InactivityTimeout, errorInfo);
                }, null, Meta.timeoutMs, Timeout.Infinite);

                this.currentRecord = new SessionRecord(this.Id, this.Meta.schedulingSeed);
                this.currentRecord.RecordBegin();
            }

            // create a continuation callback that will notify the client once the test is finished
            this.SessionFinished.Task.ContinueWith(prev => {
                //Console.WriteLine("\n[TestingSession.SessionFinished] was settled");
#if DEBUG
                Console.WriteLine(Profiler.ToString());
#endif
                // emit onComplete event
                this.OnComplete(this, prev.Result);
            });
        }

        /// <summary>
        /// Called internally to end the test. This method asynchronously resolves the <see cref="SessionFinished"/>
        /// TaskCompletionSource and returns immediately to the caller.
        /// If the test run has encountered a bug (i.e., AssertionFailure is thrown), it drops all pending requests.
        /// </summary>
        /// <param name="result">The result of the test run</param>
        /// <param name="reason">Any description of the result</param>
        private void Finish(TestResult result, string reason = "")
        {
            /*Console.WriteLine("\n[TestingSession.Finish] was called while on Task {2}, thread: {0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, this.programState.currentTask);
            Console.WriteLine("[TestingSession.Finish] Result: {0}, Reason: {1}, Already Finished: {2}", passed.ToString(), reason, this.IsFinished);*/

            // Monitor.Enter(this.stateLock);

            // Ensure the following is called at most once
            if (!this.IsFinished.Value)
            {
                this.IsFinished.Value = true;
                // Monitor.Exit(this.stateLock);

                // Drop all pending tasks if finished due to error
                if (result != TestResult.Pass)
                {
                    // Console.WriteLine("  Dropping {0} tasks", this.programState.taskToTcs.Count);
                    lock (programState)
                    {
                        foreach (var item in this.programState.taskToTcs)
                        {
                            // if (item.Key != programState.currentTask)
                            /*if (item.Key != 0)
                            {
                                Console.WriteLine("    ... dropping Session {2} Task {0} ({1})", item.Key, item.Value.Task.Status.ToString(), this.Id);
                                item.Value.TrySetException(new TestFailedException(reason));
                                this.programState.taskToTcs.Remove(item.Key);
                            }*/
#if DEBUG
                            Console.WriteLine("    ... dropping Session {2} Task {0} ({1})", item.Key, item.Value.Task.Status.ToString(), this.Id);
#endif
                            item.Value.TrySetException(new TestFailedException(reason));
                            this.programState.taskToTcs.Remove(item.Key);
                        }
                    }
                }

                // The following procedure needs to be done asynchronously
                // because this call to Finish must return to the caller
                Task.Run(() =>
                {
                    SessionRecord record = this.currentRecord;

                    // Wait for other threads to return first
                    // if there is some thread that is not returning, then there is something wrong with the user program.
                    // One last pending request is the current request: WaitForMainTask 
                    bool userProgramFaulty = false;
                    string userProgramFaultyReason = "";
                    int wait = 2000;
                    while (this.pendingRequests.Count > 0)
                    {
#if DEBUG
                        lock (this.pendingRequests)
                        {
                            Console.WriteLine("Waiting for {0} requests to complete...\t({2} Tasks still in queue){1}", this.pendingRequests.Count, String.Join("", this.pendingRequests.Select(item => "\n\t... " + item.ToString())), programState.taskToTcs.Count);
                        }
#endif
                        Thread.Sleep(50);
                        wait -= 50;
                        if (wait <= 0)
                        {
                            userProgramFaulty = true;
                            var pendingList = String.Join("", this.pendingRequests.Select(req => "\n\t  - " + req.ToString()));
                            userProgramFaultyReason = $"Could not resolve {this.pendingRequests.Count} pending requests!\n\t  waiting on:{pendingList}.\n\nPossible reasons:\n\t- Some Tasks not being modelled\n\t- Calling ContextSwitch in a Task that is not declared";
                            break;
                        }
                    }

                    // Clear timer
                    this.timeout.Change(Timeout.Infinite, Timeout.Infinite);
                    this.timeout.Dispose();

                    /*foreach (var req in this.pendingRequests)
                    {
                        req.Drop();
                    }*/
                    // this.pendingRequests.Clear();

#if DEBUG
                    Console.WriteLine("\n\nOnly {0} last request to complete...", this.pendingRequests.Count);
#endif
                    record.RecordEnd();
                    record.numDecisions = testTrace.Count;

                    if (PrintVerbosity > 1)
                    {
                        Console.WriteLine("\n===[ Decision Trace ({0} decision points) ]=====\n", testTrace.Count);
                        PrintTrace(testTrace);
                    }

                    if (testTrace.Count == 0)
                    {
                        userProgramFaulty = true;
                        userProgramFaultyReason = "There seems to be no concurrency modeled in the user program - 0 scheduling decisions were made";
                    }

                    // if result is set, then there was something wrong with the user program so we do not record the trace
                    if (userProgramFaulty)
                    {
                        // var pendingList = String.Join("", this.pendingRequests.Select(req => "\n\t  - " + req.ToString()));
                        // var errorInfo = $"Could not resolve {this.pendingRequests.Count} pending requests!\n\t  waiting on:{pendingList}.\n\nPossible reasons:\n\t- Some Tasks not being modelled\n\t- Calling ContextSwitch in a Task that is not declared";
                        
                        record.RecordResult(TestResult.FaultyProgram, userProgramFaultyReason);
                    }
                    else
                    {
                        bool reproducible = false;

                        // Write trace only if this is the first time running this session
                        if (!this.IsReplayMode)
                        {
                            record.RecordResult(result, reason);

                            this.traceFile.Write(String.Join("\n", this.testTrace.Select(step => step.ToString())));
                            this.traceFile.Close();

                            this.logFile.Write(String.Join("\n", this.runtimeLog));
                            this.logFile.Close();
                        }
                        // If it is in replay mode, compare with previous trace
                        else
                        {
                            // if the results are different, then something is wrong with the user program
                            if (this.LastRecord.result != result || this.LastRecord.reason != reason)
                            {
                                var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                record.RecordResult(TestResult.FaultyProgram, errorInfo);
                                Console.WriteLine("\n\n!!! Result Mismatch:\n  Last: {0} {1},\n   Now: {2} {3} \n", this.LastRecord.result.ToString(), this.LastRecord.reason, result.ToString(), reason);
                            }
                            else
                            {
                                var traceText = File.ReadAllText(this.traceFilePath);
                                List<DecisionTrace> previousTrace = traceText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => DecisionTrace.FromString(line)).ToList();
                                
                                //PrintTrace(previousTrace);

                                // if the traces are different, then something is wrong with the user program
                                if (this.testTrace.Count != previousTrace.Count)
                                {
                                    var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                    record.RecordResult(TestResult.FaultyProgram, errorInfo);

                                    Console.WriteLine("\n\n!!! Trace Length Mismatch: Last = {0} vs Now = {1}\n", previousTrace.Count.ToString(), this.testTrace.Count.ToString());
                                    PrintTrace(previousTrace);
                                    Console.WriteLine("----- versus -----");
                                    PrintTrace(this.testTrace);
                                }
                                else
                                {
                                    bool match = this.testTrace.Select((t, i) => previousTrace[i] == t).Aggregate(true, (acc, b) => acc && b);

                                    // if the traces are different, then something is wrong with the user program
                                    if (!match)
                                    {
                                        var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                        record.RecordResult(TestResult.FaultyProgram, errorInfo);

                                        Console.WriteLine("\n\n!!! Trace Mismatch \n");
                                        PrintTrace(previousTrace);
                                        Console.WriteLine("----- versus -----");
                                        PrintTrace(this.testTrace);
                                    }
                                    else
                                    {
                                        // Console.WriteLine("\n\t... Decision trace was reproduced successfully");
                                        record.RecordResult(result, reason);
                                        reproducible = true;
                                    }
                                }
                            }
                        }

                        lock (this.records)
                        {
                            if (!this.IsReplayMode || reproducible) this.records.Add(record);
                        }
                    }

#if DEBUG
                    //Console.WriteLine(Profiler.ToString());
#endif
                    Console.WriteLine("<<< Ending Session {0} : {1} >>>", this.Id, result.ToString());

                    // signal the end of the test, so that WaitForMainTask can return
                    this.SessionFinished.SetResult(record);
                });
            }
        }

        /// <summary>
        /// This method is called exclusively by <see cref="NekaraServer"/> to forward
        /// the serialized method invocation request from the client-side to this (<see cref="TestingSession"/>) object.
        /// Under the hood, it creates a <see cref="RemoteMethodInvocation"/> object that wraps the API method,
        /// handling any exceptions thrown during the invocation such as <see cref="AssertionFailureException"/>
        /// in the appropriate manner.
        /// Upon any invocation throwing an error, the testing session terminates by calling <see cref="Finish(bool, string)"/>,
        /// which in turn drops all other method invocations currently being executed (by throwing a <see cref="TestFailedException"/>).
        /// </summary>
        /// <param name="func">The name of the method - defined in <see cref="ITestingService"/></param>
        /// <param name="args">Any arguments to be provided for the method</param>
        /// <returns></returns>
        public object InvokeAndHandleException(string func, params object[] args)
        {
            if (this.IsFinished.Value)
            {
                // There is a possibility that the test has already finished (e.g., due to an assertion error being thrown before this method is called).
                // In this case, the result should be already available and should be returned to the client.
                if (func == "WaitForMainTask") return this.SessionFinished.Task.Result.Serialize();

                throw new SessionAlreadyFinishedException($"Session {this.Id} has already finished. Rejecting request for {Helpers.MethodInvocationString(func, args)}");
            }

            DateTime calledAt = DateTime.Now;
#if DEBUG
            var stamp = Profiler.Update(func + "Call");
#endif

            var method = typeof(TestingSession).GetMethod(func, args.Select(arg => arg.GetType()).ToArray());
            var invocation = new RemoteMethodInvocation(this, method, args);

            if (func != "WaitForMainTask")
            {
                lock (this.pendingRequests)
                {
                    this.pendingRequests.Add(invocation);
                }
            }

            invocation.OnError += (sender, ex) =>
            {
#if DEBUG
                Console.WriteLine("[TestingSession[{0}].InvokeAndHandleException] {1}", this.Id, ex.GetType().Name);
#endif
                // If the error thrown is an AssertionFailure,
                // end the test immediately. If not, ignore the error.
                // WARN: All exceptions apart from AssertionFailure will be silenced here
                if (ex is AssertionFailureException)
                {
                    this.Finish(TestResult.Fail, ex.Message);
                }
#if DEBUG
                else
                {
                    Console.WriteLine("[TestingSession.InvokeAndHandleException]");
                    Console.WriteLine(ex);
                }
#endif
            };

            invocation.OnBeforeInvoke += (sender, ev) =>
            {
                AppendLog(counter++, Thread.CurrentThread.ManagedThreadId, currentProcess.Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "enter", func, String.Join(";", args.Select(arg => arg.ToString())));
            };

            invocation.OnAfterInvoke += (sender, ev) =>
            {
                AppendLog(counter++, Thread.CurrentThread.ManagedThreadId, currentProcess.Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "exit", func, String.Join(";", args.Select(arg => arg.ToString())));

                this.currentRecord.RecordMethodCall((DateTime.Now - calledAt).TotalMilliseconds);
#if DEBUG
                stamp = Profiler.Update(func + "Return", stamp);
#endif

                if (func != "WaitForMainTask")
                {
                    lock (this.pendingRequests)
                    {
                        this.pendingRequests.Remove(invocation);
                    }
                }
            };

            try
            {
                return invocation.Invoke();
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine("\n[TestingSession.InvokeAndHandleException] rethrowing {0} !!!", ex.GetType().Name);
#endif
                if (!(ex is TestingServiceException))
                {
                    Console.WriteLine("[TestingSession.InvokeAndHandleException]");
                    Console.WriteLine(ex);
                }
                throw ex;
            }
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        
        /// <summary>
        /// Initializes the creation of a Task. When this is called, the Task has not been actually started -
        /// it simply signals that a Task is about to be created. We can only know that a Task has actually been
        /// created when the Task calls <see cref="StartTask(int)"/>.
        /// Under the hood, this call simply increments <see cref="ProgramState.numPendingTaskCreations"/>.
        /// </summary>
        public void CreateTask()
        {
#if DEBUG
            var stamp = Profiler.Update("CreateTaskBegin");
#endif
            lock (programState)
            {
                programState.InitTaskCreation();
            }
#if DEBUG
            stamp = Profiler.Update("CreateTaskEnd", stamp);
#endif
        }

        /// <summary>
        /// Called when a Task has actually been created. This call is placed at the beginning of the asynchronous Action
        /// to signal to the testing service that the Task has started. When this is called, it will immediately block
        /// and prevent the Task from progressing further, until the scheduler (this <see cref="TestingSession"/> object)
        /// gives control back to the Task.
        /// </summary>
        public void StartTask(int taskId)
        {
#if DEBUG
            var stamp = Profiler.Update("StartTaskBegin");
#endif
            TaskCompletionSource<bool> tcs;

            lock (programState)
            {
                Assert(programState.numPendingTaskCreations > 0, $"Unexpected StartTask! StartTask({taskId}) called without calling CreateTask");
                Assert(!programState.HasTask(taskId), $"Duplicate declaration of task: {taskId}");
                tcs = programState.AddTask(taskId);
            }

#if DEBUG
            stamp = Profiler.Update("StartTaskUpdate", stamp);
#endif

            tcs.Task.Wait();

#if DEBUG
            stamp = Profiler.Update("StartTaskEnd", stamp);
#endif
        }

        /// <summary>
        /// Called at the end of an asynchronous Action to signal to the testing service that
        /// the Task should be removed from the program and some other Task should be given control.
        /// If this method is not called by the user program, the scheduler will not know that
        /// the Task has ended and will assume it is still executing.
        /// </summary>
        public void EndTask(int taskId)
        {
#if DEBUG
            var stamp = Profiler.Update("EndTaskBegin");
#endif
            WaitForPendingTaskCreations();

#if DEBUG
            stamp = Profiler.Update("EndTaskWaited", stamp);
#endif

            lock (programState)
            {
                Assert(programState.HasTask(taskId), $"EndTask called on unknown task: {taskId}");
                //Console.WriteLine($"EndTask({taskId}) Status: {programState.taskToTcs[taskId].Task.Status}");

                // The following assert will fail if the user is calling
                // additional ContextSwitch without declaring creation of Tasks
                Assert(programState.taskToTcs[taskId].Task.IsCompleted,
                    $"EndTask called but Task ({taskId}) did not receive control before (Task.Status == WaitingForActivation). Something is wrong with the test program - maybe ContextSwitch is called from an unmodelled Task?",
                    ()=> programState.taskToTcs[taskId].TrySetException(new TestFailedException($"EndTask called but Task ({taskId}) did not receive control before (Task.Status == WaitingForActivation)")));
                programState.RemoveTask(taskId);
            }

#if DEBUG
            stamp = Profiler.Update("EndTaskUpdate", stamp);
#endif

            ContextSwitch();

#if DEBUG
            stamp = Profiler.Update("EndTaskEnd", stamp);
#endif
        }

        /// <summary>
        /// Used to register an arbitrary "resource" through which different Tasks can synchronize.
        /// The "resource" can be the Task itself, a network socket, a shared object, or any arbitrary object that
        /// the user program considers relevant for synchronization.
        /// </summary>
        public void CreateResource(int resourceId)
        {
#if DEBUG
            var stamp = Profiler.Update("CreateResourceBegin");
#endif

            lock (programState)
            {
                Assert(!programState.HasResource(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.AddResource(resourceId);
            }

#if DEBUG
            stamp = Profiler.Update("CreateResourceEnd", stamp);
#endif
        }

        /// <summary>
        /// Used to remove a declared resource from the program state.
        /// </summary>
        public void DeleteResource(int resourceId)
        {
#if DEBUG
            var stamp = Profiler.Update("DeleteResourceBegin");
#endif

            lock (programState)
            {
                Assert(programState.HasResource(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                Assert(programState.SafeToDeleteResource(resourceId), $"DeleteResource called on resource {resourceId} but some tasks are blocked on it");
                programState.resourceSet.Remove(resourceId);
            }
#if DEBUG
            stamp = Profiler.Update("DeleteResourceEnd", stamp);
#endif
        }

        /// <summary>
        /// This method is used to block a Task based on the status of a previously declared resource.
        /// It is usually called conditionally, by first checking if the said resource is available or not,
        /// then calling it only if the resource is unavailble.
        /// At a high level, this method can be seen as a "synchronous event listener"
        /// that returns (unblocks) when the resource-owning Task emits a "resource available" event.
        /// </summary>
        /// <param name="resourceId"></param>
        public void BlockedOnResource(int resourceId)
        {
#if DEBUG
            var stamp = Profiler.Update("BlockedOnResourceBegin");
#endif
            lock (programState)
            {
                Assert(programState.HasResource(resourceId), $"Illegal operation, resource {resourceId} has not been declared");
                Assert(!programState.IsBlockedOnTask(programState.currentTask),
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.BlockTaskOnAnyResource(programState.currentTask, resourceId);
            }
#if DEBUG
            stamp = Profiler.Update("BlockedOnResourceUpdate", stamp);
#endif

            ContextSwitch();
#if DEBUG
            stamp = Profiler.Update("BlockedOnResourceEnd", stamp);
#endif
        }

        /// <summary>
        /// Similar to <see cref="BlockedOnResource(int)"/>, this method is used to block a Task based on a set of resources.
        /// The call will return if any one of the give resources are updated.
        /// </summary>
        public void BlockedOnAnyResource(params int[] resourceIds)
        {
#if DEBUG
            var stamp = Profiler.Update("BlockedOnAnyResourceBegin");
#endif
            lock (programState)
            {
                foreach (int resourceId in resourceIds)
                {
                    Assert(programState.HasResource(resourceId), $"Illegal operation, resource {resourceId} has not been declared");
                }
                Assert(!programState.IsBlockedOnTask(programState.currentTask),
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.BlockTaskOnAnyResource(programState.currentTask, resourceIds);
            }
#if DEBUG
            stamp = Profiler.Update("BlockedOnResourceUpdate", stamp);
#endif

            ContextSwitch();
#if DEBUG
            stamp = Profiler.Update("BlockedOnResourceEnd", stamp);
#endif
        }

        /// <summary>
        /// This method is the counterpart of <see cref="BlockedOnResource(int)"/>, and is called by the resource-owning Task.
        /// This is used to signal any other Tasks blocked on the given resource that the resource is now available.
        /// At a high level, this method can be seen as an event emitter.
        /// </summary>
        public void SignalUpdatedResource(int resourceId)
        {
#if DEBUG
            var stamp = Profiler.Update("SignalUpdatedResourceBegin");
#endif

            lock (programState)
            {
                Assert(programState.HasResource(resourceId), $"Illegal operation, resource {resourceId} has not been declared");

                var blockedTasks = programState.blockedTasks.Where(tup => tup.Value.Contains(resourceId)).Select(tup => tup.Key).ToList();
                foreach (var taskId in blockedTasks)
                {
                    programState.UnblockTask(taskId);
                }
            }

#if DEBUG
            stamp = Profiler.Update("SignalUpdatedResourceEnd", stamp);
#endif
        }

        /// <summary>
        /// Generate a random boolean value. This is a non-deterministic scheduling decision, and is recorded in the decision trace.
        /// </summary>
        public bool CreateNondetBool()
        {
#if DEBUG
            var stamp = Profiler.Update("CreateNondetBoolBegin");
#endif
            lock (programState)
            {
                bool value = this.randomizer.NextBool();
                this.PushTrace(DecisionType.CreateNondetBool, value ? 1 : 0, programState.currentTask, programState);
#if DEBUG
                stamp = Profiler.Update("CreateNondetBoolEnd", stamp);
#endif
                return value;
            }
        }

        /// <summary>
        /// Generate a random integer value. This is a non-deterministic scheduling decision, and is recorded in the decision trace.
        /// </summary>
        public int CreateNondetInteger(int maxValue)
        {
#if DEBUG
            var stamp = Profiler.Update("CreateNondetIntegerBegin");
#endif
            lock (programState)
            {
                int value = this.randomizer.NextInt(maxValue);
                this.PushTrace(DecisionType.CreateNondetInteger, value, programState.currentTask, programState);
#if DEBUG
                stamp = Profiler.Update("CreateNondetIntegerEnd", stamp);
#endif
                return value;
            }
        }

        /// <summary>
        /// Called by the user program and also internally to assert a certain expression.
        /// If the assertion fails, this method throws an <see cref="AssertionFailureException"/>
        /// and the test will terminate as a result.
        /// </summary>
        public void Assert(bool value, string message)
        {
            if (!value)
            {
                throw new AssertionFailureException(message);
            }
        }

        /// <summary>
        /// Called only internally to assert a certain expression, then invoke a callback if the assert fails.
        /// If the assertion fails, this method throws an <see cref="AssertionFailureException"/>
        /// and the test will terminate as a result.
        /// </summary>
        public void Assert(bool value, string message, Action onError)
        {
            if (!value)
            {
                onError();
                throw new AssertionFailureException(message);
            }
        }

        /// <summary>
        /// Used to indicate a scheduling point in the program. It can be called directly from the user program,
        /// and is also called internally by <see cref="EndTask(int)"/> and <see cref="BlockedOnResource(int)"/>.
        /// When it is called, it yields control to the scheduler and blocks execution until the scheduler returns control to it.
        /// The majority of scheduling logic resides in this method.
        /// </summary>
        public void ContextSwitch()
        {
#if DEBUG
            var stamp = Profiler.Update("ContextSwitchBegin");
#endif

            WaitForPendingTaskCreations();

#if DEBUG
            stamp = Profiler.Update("ContextSwitchWaited", stamp);
#endif

            var tcs = new TaskCompletionSource<bool>();
            int[] enabledTasks;
            int next;
            int currentTask;
            bool currentTaskStillRunning = false;

            lock (programState)
            {
                currentTask = programState.currentTask;
                currentTaskStillRunning = programState.taskToTcs.ContainsKey(currentTask);

                // pick next one to execute
                enabledTasks = programState.taskToTcs.Keys
                    .Where(k => !programState.blockedTasks.ContainsKey(k))
                    .OrderBy(k => k)
                    .ToArray();

                if (enabledTasks.Length == 0)
                {
                    // if all remaining tasks are blocked then it's deadlock
                    Assert(programState.taskToTcs.Count == 0, "Deadlock detected");

                    // if there are no blocked tasks and the task set is empty, we are all done
                    this.Finish(TestResult.Pass);
                    return;
                }

                next = this.randomizer.NextInt(enabledTasks.Length);
            }

            // record the decision at this point, before making changes to the programState
            lock (programState) {
                // Assert(this.testTrace.Count < Meta.maxDecisions, "Maximum steps reached; the program may be in a live-lock state!");
                if (this.testTrace.Count >= Meta.maxDecisions)
                {
                    // throw new MaximumDecisionPointsReachedException("Maximum steps reached; the program might be in a live-lock state! (or the program might be a non-terminating program)");
                    this.Finish(TestResult.MaxDecisionsReached, "Maximum steps reached; the program might be in a live-lock state! (or the program might be a non-terminating program)");
                    return;
                }
                AppendLog(counter, Thread.CurrentThread.ManagedThreadId, currentProcess.Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "-", "*ContextSwitch*", $"{currentTask} --> {enabledTasks[next]}");

                this.PushTrace(DecisionType.ContextSwitch, enabledTasks[next], currentTask, programState);
                this.timeout.Change(Meta.timeoutMs, Timeout.Infinite);

#if DEBUG
                stamp = Profiler.Update("ContextSwitchDecisionMade", stamp);
#endif
            }

            // if current task is not blocked and is selected, just continue
            if (enabledTasks[next] == currentTask)
            {
                // no-op
                if (PrintVerbosity > 1) Console.WriteLine($"[{programState.GetCurrentStateString()}]");
            }
            else
            {
                // if the selected task is not the current task,
                // need to give control to the selected task
                TaskCompletionSource<bool> nextTcs;

                lock (programState)
                {
                    if (PrintVerbosity > 1) Console.WriteLine($"[{programState.GetCurrentStateString()}]\t{currentTask} ---> {enabledTasks[next]}");
                    else if (PrintVerbosity > 0) Console.Write(".");

                    // get the tcs of the selected task
                    nextTcs = programState.taskToTcs[enabledTasks[next]];

                    // if current task is still running, save the new tcs
                    // so that we can retrieve the control back
                    if (currentTaskStillRunning)
                    {
                        // The following assert will fail if the user is calling
                        // additional ContextSwitch without declaring creation of Tasks
                        Assert(programState.taskToTcs[currentTask].Task.IsCompleted,
                            $"ContextSwitch called but current Task ({currentTask}) did not receive control before (Task.Status == WaitingForActivation). Something is wrong with the test program - maybe ContextSwitch is called from an unmodelled Task?",
                            () => programState.taskToTcs[currentTask].TrySetException(new TestFailedException($"ContextSwitch called but current Task ({currentTask}) did not receive control before (Task.Status == WaitingForActivation)")));

                        programState.taskToTcs[currentTask] = tcs;
                    }

                    // update the current task
                    programState.currentTask = enabledTasks[next];
                }

#if DEBUG
                stamp = Profiler.Update("ContextSwitchUpdate", stamp);
#endif

                // complete the tcs to let the task continue
                nextTcs.SetResult(true);

                if (currentTaskStillRunning)
                {
                    // block the current task until it is resumed by a future ContextSwitch call
                    tcs.Task.Wait();
#if DEBUG
                    stamp = Profiler.Update("ContextSwitchEnd", stamp);
#endif
                }
            }
        }

        /// <summary>
        /// Called internally to wait until all declared Tasks are created.
        /// </summary>
        void WaitForPendingTaskCreations()
        {
            while (true)
            {
                lock (programState)
                {
                    if (programState.numPendingTaskCreations == 0)
                    {
                        return;
                    }
                }

                Thread.Sleep(TimeSpan.Zero);
            }
        }

        /// <summary>
        /// Called by the testing client to signal the end of the main test method.
        /// The server needs this information because it does not know when all Tasks
        /// in the program have been declared. If this is not called by the client,
        /// the server will wait indefinitely for further Task declarations.
        /// The call will not return until the test has finished either by exhausting
        /// all the Tasks or by finding a bug via AssertionFailure.
        /// </summary>
        /// <returns></returns>
        public string WaitForMainTask()
        {
            //AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "enter", "*EndTask*", 0);

            try
            {
                this.EndTask(0);
            }
            catch (TestingServiceException ex)
            {
                this.Finish(TestResult.Error, ex.Message);
            }
            //AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "exit", "*EndTask*", 0);

            var result = this.SessionFinished.Task.Result;

            return result.Serialize();
        }
    }
}