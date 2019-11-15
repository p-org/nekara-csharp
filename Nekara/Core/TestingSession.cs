using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Nekara.Core
{
    public class TestingSession : ITestingService
    {
        // It appears that Process.GetCurrentProcess is a very expensive call
        // (makes the whole app 10 ~ 15x slower when called in AppendLog), so we save the reference here.
        // However, this object is used only for debugging and can be omitted entirely.
        private static Process currentProcess = Process.GetCurrentProcess();
        private static int PrintVerbosity = 0;

#if DEBUG
        public static Helpers.MicroProfiler Profiler = new Helpers.MicroProfiler();
#endif

        private static Helpers.UniqueIdGenerator IdGen = new Helpers.UniqueIdGenerator(true, 1);

        // metadata
        public SessionInfo Meta;

        // run-time objects
        private readonly StreamWriter traceFile;
        private readonly StreamWriter logFile;
        private Action<SessionRecord> _onComplete;
        private readonly object stateLock;
        private Timer timeout;

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
            this._onComplete = record => { };     // empty onComplete handler
            this.stateLock = new object();
            this.traceFile = File.AppendText(this.traceFilePath);
            this.logFile = File.AppendText(this.logFilePath);

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

        public bool IsFinished { get; set; }

        public bool IsReplayMode { get { return this.records.Count > 0; } }

        public SessionRecord LastRecord { get { return this.records.Last(); } }

        public void OnComplete(Action<SessionRecord> action)
        {
            this._onComplete = action;
        }

        private void PushTrace(DecisionType decisionType, int decisionValue, int currentTask, ProgramState state)
        {
            lock (this.testTrace)
            {
                // (int, int)[] tasks = state.taskToTcs.Keys.Select(taskId => state.blockedTasks.ContainsKey(taskId) ? (taskId, state.blockedTasks[taskId]) : (taskId, -1)).ToArray();
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

        private void PrintTrace(List<DecisionTrace> list)
        {
            list.ForEach(trace => Console.WriteLine(trace.ToReadableString()));
        }

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
                this.IsFinished = false;
                this.timeout = new Timer(_ =>
                {
                    string currentTask = this.programState.currentTask.ToString();
                    var errorInfo = $"No activity for {(Meta.timeoutMs / 1000).ToString()} seconds!\n  Program State: [ {this.programState.GetCurrentStateString()} ]\n  Possible reasons:\n\t- Not calling EndTask({currentTask})\n\t- Calling ContextSwitch from an undeclared Task\n\t- Some Tasks not being modelled";
                    this.Finish(false, errorInfo);
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
                this._onComplete(prev.Result);
            });
        }

        public void Finish(bool passed, string reason = "")
        {
            /*Console.WriteLine("\n[TestingSession.Finish] was called while on Task {2}, thread: {0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, this.programState.currentTask);
            Console.WriteLine("[TestingSession.Finish] Result: {0}, Reason: {1}, Already Finished: {2}", passed.ToString(), reason, this.IsFinished);*/

            Monitor.Enter(this.stateLock);

            // Ensure the following is called at most once
            if (!this.IsFinished)
            {
                this.IsFinished = true;
                Monitor.Exit(this.stateLock);

                // Drop all pending tasks if finished due to error
                if (!passed)
                {
                    // Console.WriteLine("  Dropping {0} tasks", this.programState.taskToTcs.Count);
                    lock (programState)
                    {
                        foreach (var item in this.programState.taskToTcs)
                        {
                            // if (item.Key != programState.currentTask)
                            if (item.Key != 0)
                            {
                                Console.WriteLine("    ... dropping Task {0} ({1})", item.Key, item.Value.Task.Status.ToString());
                                item.Value.TrySetException(new TestFailedException(reason));
                                this.programState.taskToTcs.Remove(item.Key);
                            }
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
                        /*lock (this.pendingRequests)
                        {
                            Console.WriteLine("Waiting for {0} requests to complete...\t({2} Tasks still in queue){1}", this.pendingRequests.Count, String.Join("", this.pendingRequests.Select(item => "\n\t... " + item.ToString())), programState.taskToTcs.Count);
                        }*/
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

                    //Console.WriteLine("\n\nOnly {0} last request to complete...", this.pendingRequests.Count);

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
                        
                        record.RecordResult(false, userProgramFaultyReason);
                    }
                    else
                    {
                        bool reproducible = false;

                        // Write trace only if this is the first time running this session
                        if (!this.IsReplayMode)
                        {
                            record.RecordResult(passed, reason);

                            this.traceFile.Write(String.Join("\n", this.testTrace.Select(step => step.ToString())));
                            this.traceFile.Close();

                            this.logFile.Write(String.Join("\n", this.runtimeLog));
                            this.logFile.Close();
                        }
                        // If it is in replay mode, compare with previous trace
                        else
                        {
                            // if the results are different, then something is wrong with the user program
                            if (this.LastRecord.passed != passed || this.LastRecord.reason != reason)
                            {
                                var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                record.RecordResult(false, errorInfo);
                                Console.WriteLine("\n\n!!! Result Mismatch:\n  Last: {0} {1},\n   Now: {2} {3} \n", this.LastRecord.passed.ToString(), this.LastRecord.reason, passed.ToString(), reason);
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
                                    record.RecordResult(false, errorInfo);

                                    Console.WriteLine("\n\n!!! Trace Length Mismatch: Last = {0} vs Now = {1}\n", this.testTrace.Count.ToString(), previousTrace.Count.ToString());
                                }
                                else
                                {
                                    bool match = this.testTrace.Select((t, i) => previousTrace[i] == t).Aggregate(true, (acc, b) => acc && b);

                                    // if the traces are different, then something is wrong with the user program
                                    if (!match)
                                    {
                                        var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                        record.RecordResult(false, errorInfo);

                                        Console.WriteLine("\n\n!!! Trace Mismatch \n");
                                        PrintTrace(previousTrace);
                                        Console.WriteLine("----- versus -----");
                                        PrintTrace(this.testTrace);
                                    }
                                    else
                                    {
                                        // Console.WriteLine("\n\t... Decision trace was reproduced successfully");
                                        record.RecordResult(passed, reason);
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

                    // signal the end of the test, so that WaitForMainTask can return
                    this.SessionFinished.SetResult(record);
                });
            }
            else Monitor.Exit(this.stateLock);
        }

        public object InvokeAndHandleException(string func, params object[] args)
        {
            if (this.IsFinished) throw new SessionAlreadyFinishedException("Session " + this.Id + " has already finished");

            DateTime calledAt = DateTime.Now;
            var stamp = Profiler.Update(func + "Call");

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
                Console.WriteLine("[InvokeAndHandleException] {0}", ex.GetType().Name);
                if (ex is AssertionFailureException)
                {
                    this.Finish(false, ex.Message);
                }
            };

            invocation.OnBeforeInvoke += (sender, ev) =>
            {
                AppendLog(counter++, Thread.CurrentThread.ManagedThreadId, currentProcess.Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "enter", func, String.Join(";", args.Select(arg => arg.ToString())));
            };

            invocation.OnAfterInvoke += (sender, ev) =>
            {
                AppendLog(counter++, Thread.CurrentThread.ManagedThreadId, currentProcess.Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "exit", func, String.Join(";", args.Select(arg => arg.ToString())));

                this.currentRecord.RecordMethodCall((DateTime.Now - calledAt).TotalMilliseconds);
                stamp = Profiler.Update(func + "Return", stamp);

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
                Console.WriteLine("\n[TestingSession.InvokeAndHandleException] rethrowing {0} !!!", ex.GetType().Name);
                if (!(ex is TestingServiceException))
                {
                    Console.WriteLine(ex);
                }
                throw ex;
            }
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
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

        public void SignalUpdatedResource(int resourceId)
        {
#if DEBUG
            var stamp = Profiler.Update("SignalUpdatedResourceBegin");
#endif

            lock (programState)
            {
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

        public void Assert(bool value, string message)
        {
            if (!value)
            {
                throw new AssertionFailureException(message);
            }
        }

        public void Assert(bool value, string message, Action onError)
        {
            if (!value)
            {
                onError();
                throw new AssertionFailureException(message);
            }
        }

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
                    this.Finish(true);
                    return;
                }

                next = this.randomizer.NextInt(enabledTasks.Length);
            }

            // record the decision at this point, before making changes to the programState
            lock (programState) {
                Assert(this.testTrace.Count < Meta.maxDecisions, "Maximum steps reached; the program may be in a live-lock state!");
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
                    else Console.Write(".");

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

                Thread.Sleep(1);
            }
        }

        public string WaitForMainTask()
        {
            //AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "enter", "*EndTask*", 0);
            try
            {
                this.EndTask(0);
            }
            catch (TestingServiceException ex)
            {
                this.Finish(false, ex.Message);
            }
            //AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "exit", "*EndTask*", 0);

            var result = this.SessionFinished.Task.Result;

            return result.Serialize();
        }
    }
}