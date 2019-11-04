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
        // hyperparameters
        private static int timeoutDelay = 5000;     // timeout for client-side inactivity
        private static int maxDecisions = 500;     // threshold for determining live-lock

        // metadata
        public SessionInfo Info;

        // run-time objects
        public StreamWriter logger;
        private readonly StreamWriter traceFile;
        private Action<TestingSession> _onComplete;
        private readonly object stateLock;
        private bool replayMode; // indicates it has already ran once
        private DateTime startedAt;
        private DateTime finishedAt;
        private TimeSpan elapsed;
        private Timer timeout;

        // testing service objects
        private Helpers.SeededRandomizer randomizer;
        private int counter;
        ProgramState programState;
        int numPendingTaskCreations;
        TaskCompletionSource<TestResult> SessionFinished;
        HashSet<RemoteMethodInvocation> pendingRequests;    // to keep track of unresolved requests. This can actually be done with a simple counter, but we want to present useful information to the client.
        List<DecisionTrace> testTrace;

        // result data
        public TestResult lastResult;

        public TestingSession(string assemblyName, string assemblyPath, string methodDeclaringClass, string methodName, int schedulingSeed)
        {

            this.Info = new SessionInfo(Helpers.RandomString(8), assemblyName, assemblyPath, methodDeclaringClass, methodName, schedulingSeed);

            this.lastResult = default(TestResult);

            // initialize run-time objects
            this._onComplete = self => { };     // empty onComplete handler
            this.stateLock = new object();
            this.replayMode = false;
            this.traceFile = File.AppendText("logs/trace-" + this.Id + ".csv");

            this.Reset();
        }

        public string Id { get { return this.Info.id; } }

        // public SessionInfo info { get { return new SessionInfo(this.id, this.assemblyName, this.assemblyPath, this.methodDeclaringClass, this.methodName, this.schedulingSeed); } }

        public bool IsFinished { get; set; }

        public double ElapsedMilliseconds { get { return this.elapsed.TotalMilliseconds; } }
        public double ElapsedSeconds { get { return this.elapsed.TotalSeconds; } }

        public void OnComplete(Action<TestingSession> action)
        {
            this._onComplete = action;
        }

        private void PushTrace(int currentTask, int chosenTask, ProgramState state)
        {
            lock (this.testTrace)
            {
                (int, int)[] tasks = state.taskToTcs.Keys.Select(taskId => state.blockedTasks.ContainsKey(taskId) ? (taskId, state.blockedTasks[taskId]) : (taskId, -1)).ToArray();
                var decision = new DecisionTrace(currentTask, chosenTask, tasks);
                // Console.WriteLine(decision.ToReadableString());
                this.testTrace.Add(decision);
            }
        }

        private void AppendLog(params object[] cols)
        {
            lock (this.logger)
            {
                Console.WriteLine(String.Join("\t", cols.Select(obj => obj.ToString())));
                this.logger.WriteLine(String.Join(",", cols.Select(obj => obj.ToString())));
            }
        }

        private void PrintTrace(List<DecisionTrace> list)
        {
            list.ForEach(trace => Console.WriteLine(trace.ToReadableString()));
        }

        public void Finish(bool passed, string reason = "")
        {
            Console.WriteLine("\n[TestingSession.Finish] was called while on Task {2}, thread: {0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, this.programState.currentTask);
            Console.WriteLine("[TestingSession.Finish] Result: {0}, Reason: {1}, Already Finished: {2}", passed.ToString(), reason, this.IsFinished);

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
                    TestResult result;

                    // Wait for other threads to return first
                    // if there is some thread that is not returning, then there is something wrong with the user program.
                    // One last pending request is the current request: WaitForMainTask 
                    bool userProgramFaulty = false;
                    int wait = 2000;
                    while (this.pendingRequests.Count > 0)
                    {
                        lock (this.pendingRequests)
                        {
                            Console.WriteLine("Waiting for {0} requests to complete...\t({2} Tasks still in queue){1}", this.pendingRequests.Count, String.Join("", this.pendingRequests.Select(item => "\n\t... " + item.ToString())), programState.taskToTcs.Count);
                        }
                        Thread.Sleep(50);
                        wait -= 50;
                        if (wait <= 0)
                        {
                            userProgramFaulty = true;
                            break;
                        }
                    }

                    // Clear timer
                    this.timeout.Change(Timeout.Infinite, Timeout.Infinite);
                    this.timeout.Dispose();

                    Console.WriteLine("\n\nOnly {0} last request to complete...", this.pendingRequests.Count);

                    this.finishedAt = DateTime.Now;
                    this.elapsed = this.finishedAt - this.startedAt;

                    Console.WriteLine("\n===[ Decision Trace ({0} decision points) ]=====\n", testTrace.Count);
                    PrintTrace(testTrace);

                    // if result is set, then there was something wrong with the user program so we do not record the trace
                    if (userProgramFaulty)
                    {
                        var pendingList = String.Join("", this.pendingRequests.Select(req => "\n\t  - " + req.ToString()));
                        var errorInfo = $"Could not resolve {this.pendingRequests.Count} pending requests!\n\t  waiting on:{pendingList}.\n\nPossible reasons:\n\t- Some Tasks not being modelled\n\t- Calling ContextSwitch in a Task that is not declared";
                        result = new TestResult(this.Info.schedulingSeed, false, errorInfo, this.elapsed.TotalMilliseconds);
                    }
                    else
                    {
                        // Write trace only if this is the first time running this session
                        if (this.replayMode == false)
                        {
                            result = new TestResult(this.Info.schedulingSeed, passed, reason, this.elapsed.TotalMilliseconds);

                            this.traceFile.Write(String.Join("\n", this.testTrace.Select(step => step.ToString())));
                            this.traceFile.Close();
                        }
                        // If it is in replay mode, compare with previous trace
                        else
                        {
                            // if the results are different, then something is wrong with the user program
                            // if (this.passed != passed || this.reason != reason)
                            if (this.lastResult.passed != passed || this.lastResult.reason != reason)
                            {
                                var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                result = new TestResult(this.Info.schedulingSeed, false, "Could not reproduce the trace for Session " + this.Id, this.elapsed.TotalMilliseconds);
                                Console.WriteLine("\n\n!!! Result Mismatch: {0} {1},  {2} {3} \n", this.lastResult.passed.ToString(), this.lastResult.reason, passed.ToString(), reason);
                            }
                            else
                            {
                                var traceText = File.ReadAllText("logs/trace-" + this.Id + ".csv");
                                List<DecisionTrace> previousTrace = traceText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(line => DecisionTrace.FromString(line)).ToList();

                                // if the traces are different, then something is wrong with the user program
                                if (this.testTrace.Count != previousTrace.Count)
                                {
                                    // throw new TraceReproductionFailureException("Could not reproduce the trace for Session " + this.id);
                                    var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                    result = new TestResult(this.Info.schedulingSeed, false, errorInfo, this.elapsed.TotalMilliseconds);
                                    Console.WriteLine("\n\n!!! Trace Length Mismatch: {0} vs {1}\n", this.testTrace.Count.ToString(), previousTrace.Count.ToString());
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
                                        // throw new TraceReproductionFailureException("Could not reproduce the trace for Session " + this.id);
                                        var errorInfo = $"Could not reproduce the trace for Session {this.Id}.\nPossible reasons:\n\t- Some Tasks not being modelled";
                                        result = new TestResult(this.Info.schedulingSeed, false, errorInfo, this.elapsed.TotalMilliseconds);
                                        Console.WriteLine("\n\n!!! Trace Mismatch \n");
                                        PrintTrace(previousTrace);
                                        Console.WriteLine("----- versus -----");
                                        PrintTrace(this.testTrace);
                                    }
                                    else
                                    {
                                        Console.WriteLine("\n\t... Decision trace was reproduced successfully");
                                        result = new TestResult(this.Info.schedulingSeed, passed, reason, this.elapsed.TotalMilliseconds);
                                    }
                                }
                            }
                        }

                        this.lastResult = result;

                        this.replayMode = true;
                    }

                    // signal the end of the test, so that WaitForMainTask can return
                    this.SessionFinished.SetResult(result);
                });
            }
            else Monitor.Exit(this.stateLock);
        }

        public void Reset()
        {
            lock (this.stateLock)
            {
                // reset run-time objects
                this.randomizer = new Helpers.SeededRandomizer(this.Info.schedulingSeed);
                this.counter = 0;
                this.programState = new ProgramState();
                this.numPendingTaskCreations = 0;
                this.SessionFinished = new TaskCompletionSource<TestResult>();
                this.pendingRequests = new HashSet<RemoteMethodInvocation>();
                this.testTrace = new List<DecisionTrace>();
                this.IsFinished = false;
                this.timeout = new Timer(_ =>
                {
                    string currentTask = this.programState.currentTask.ToString();
                    var errorInfo = $"No activity for {(timeoutDelay / 1000).ToString()} seconds! Currently on task {currentTask}.\nPossible reasons:\n\t- Not calling EndTask({currentTask})\n\t- Calling ContextSwitch from an undeclared Task\n\t- Some Tasks not being modelled";
                    this.Finish(false, errorInfo);
                }, null, timeoutDelay, Timeout.Infinite);

                this.startedAt = DateTime.Now;
            }

            // create a continuation callback that will notify the client once the test is finished
            this.SessionFinished.Task.ContinueWith(prev => {
                Console.WriteLine("\n[TestingSession.SessionFinished] was settled");

                // emit onComplete event
                this._onComplete(this);
            });
        }

        public object InvokeAndHandleException(string func, params object[] args)
        {
            if (this.IsFinished) throw new SessionAlreadyFinishedException("Session " + this.Id + " has already finished");

            var method = this.GetType().GetMethod(func, args.Select(arg => arg.GetType()).ToArray());
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
                Console.WriteLine("[InvokeAndHandleException] Finishing due to {0}", ex.GetType().Name);
                if (ex is AssertionFailureException) this.Finish(false, ex.Message);
            };

            invocation.OnBeforeInvoke += (sender, ev) =>
            {
                AppendLog(counter++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "enter", func, String.Join(";", args.Select(arg => arg.ToString())));
            };

            invocation.OnAfterInvoke += (sender, ev) =>
            {
                AppendLog(counter++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "exit", func, String.Join(";", args.Select(arg => arg.ToString())));
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
            catch (Exception e)
            {
                Console.WriteLine("\n[TestingSession.InvokeAndHandleException] rethrowing {0} !!!", e.GetType().Name);
                throw e;
            }
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        public void CreateTask()
        {
            lock (programState)
            {
                this.numPendingTaskCreations++;
            }
        }

        public void StartTask(int taskId)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lock (programState)
            {
                Assert(numPendingTaskCreations > 0, $"Unexpected StartTask! StartTask({taskId}) called without calling CreateTask");
                Assert(!programState.taskToTcs.ContainsKey(taskId), $"Duplicate declaration of task: {taskId}");
                programState.taskToTcs.Add(taskId, tcs);
                numPendingTaskCreations--;
                // Console.WriteLine("{0}\tTask {1}\tcreated", counter, taskId);
            }

            tcs.Task.Wait();
        }

        public void EndTask(int taskId)
        {
            WaitForPendingTaskCreations();

            lock (programState)
            {
                Assert(programState.taskToTcs.ContainsKey(taskId), $"EndTask called on unknown task: {taskId}");
                Console.WriteLine($"EndTask({taskId}) Status: {programState.taskToTcs[taskId].Task.Status}");

                // The following assert will fail if the user is calling
                // additional ContextSwitch without declaring creation of Tasks
                Assert(programState.taskToTcs[taskId].Task.IsCompleted,
                    $"EndTask called but Task ({taskId}) did not receive control before (Task.Status == WaitingForActivation). Something is wrong with the test program - maybe ContextSwitch is called from an unmodelled Task?",
                    ()=> programState.taskToTcs[taskId].TrySetException(new TestFailedException($"EndTask called but Task ({taskId}) did not receive control before (Task.Status == WaitingForActivation)")));
                programState.taskToTcs.Remove(taskId);
            }

            ContextSwitch();
        }

        public void CreateResource(int resourceId)
        {
            lock (programState)
            {
                Assert(!programState.resourceSet.Contains(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.resourceSet.Add(resourceId);
            }
        }

        public void DeleteResource(int resourceId)
        {
            lock (programState)
            {
                Assert(programState.resourceSet.Contains(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                programState.resourceSet.Remove(resourceId);
            }
        }

        public void BlockedOnResource(int resourceId)
        {
            lock (programState)
            {
                Assert(programState.resourceSet.Contains(resourceId),
                    $"Illegal operation, resource {resourceId} has not been declared");
                Assert(!programState.blockedTasks.ContainsKey(programState.currentTask),
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.blockedTasks[programState.currentTask] = resourceId;
            }

            ContextSwitch();
        }

        public void SignalUpdatedResource(int resourceId)
        {
            lock (programState)
            {
                var enabledTasks = programState.blockedTasks.Where(tup => tup.Value == resourceId).Select(tup => tup.Key).ToList();
                foreach (var k in enabledTasks)
                {
                    programState.blockedTasks.Remove(k);
                }
            }
        }

        public bool CreateNondetBool()
        {
            return this.randomizer.NextBool();
        }

        public int CreateNondetInteger(int maxValue)
        {
            return this.randomizer.NextInt(maxValue);
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
            WaitForPendingTaskCreations();

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
                    .Where(k => !programState.blockedTasks.ContainsKey(k)).ToArray();

                if (enabledTasks.Length == 0)
                {
                    // if all remaining tasks are blocked then it's deadlock
                    Assert(programState.taskToTcs.Count == 0, "Deadlock detected");

                    // if there are no blocked tasks and the task set is empty, we are all done
                    // IterFinished.SetResult(new TestResult(true));
                    this.Finish(true);
                    return;
                }

                next = this.randomizer.NextInt(enabledTasks.Length);
            }

            // record the decision at this point, before making changes to the programState
            lock (programState) {
                Assert(this.testTrace.Count < maxDecisions, "Maximum steps reached; the program may be in a live-lock state!");
                AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "-", "*ContextSwitch*", $"{currentTask} --> {enabledTasks[next]}");

                this.PushTrace(currentTask, enabledTasks[next], programState);
                this.timeout.Change(timeoutDelay, Timeout.Infinite);
            }

            // if current task is not blocked and is selected, just continue
            if (enabledTasks[next] == currentTask)
            {
                // no-op
            }
            else
            {
                // if the selected task is not the current task,
                // need to give control to the selected task
                TaskCompletionSource<bool> nextTcs;

                lock (programState)
                {
                    // get the tcs of the selected task
                    nextTcs = programState.taskToTcs[enabledTasks[next]];

                    // if current task is still running, save the new tcs
                    // so that we can retrieve the control back
                    if (currentTaskStillRunning)
                    {
                        Console.WriteLine($"ContextSwitch {currentTask} -> {enabledTasks[next]}\tCurrent Task: {programState.taskToTcs[currentTask].Task.Status}");

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

                // complete the tcs to let the task continue
                nextTcs.SetResult(true);

                if (currentTaskStillRunning)
                {
                    // block the current task until it is resumed by a future ContextSwitch call
                    tcs.Task.Wait();
                }
            }
        }

        void WaitForPendingTaskCreations()
        {
            while (true)
            {
                lock (programState)
                {
                    if (numPendingTaskCreations == 0)
                    {
                        return;
                    }
                }

                Thread.Sleep(10);
            }
        }

        public string WaitForMainTask()
        {
            AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "enter", "*EndTask*", 0);
            try
            {
                this.EndTask(0);
            }
            catch (TestingServiceException ex)
            {
                this.Finish(false, ex.Message);
            }
            AppendLog(counter, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.currentTask, programState.taskToTcs.Count, programState.blockedTasks.Count, "exit", "*EndTask*", 0);

            var result = this.SessionFinished.Task.Result;

            return result.Serialize();
        }
    }
}