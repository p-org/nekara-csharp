using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AsyncTester.Core
{
    public struct SessionInfo
    {
        public string id;
        public string assemblyName;
        public string assemblyPath;
        public string methodDeclaringClass;
        public string methodName;
        public int schedulingSeed;

        public SessionInfo(string id, string assemblyName, string assemblyPath, string methodDeclaringClass, string methodName, int schedulingSeed)
        {
            this.id = id;
            this.assemblyName = assemblyName;
            this.assemblyPath = assemblyPath;
            this.methodDeclaringClass = methodDeclaringClass;
            this.methodName = methodName;
            this.schedulingSeed = schedulingSeed;
    }
    }

    public struct TestResult
    {
        public bool passed;
        public string reason;
        public TestResult(bool passed, string reason = "")
        {
            this.passed = passed;
            this.reason = reason;
        }
    }

    public struct TraceStep
    {
        public string methodName;
        public JToken[] args;
        public JToken result;

        public TraceStep(string methodName, JToken[] args, JToken result)
        {
            this.methodName = methodName;
            this.args = args;
            this.result = result;
        }

        public static TraceStep FromString(string line)
        {
            string[] cols = line.Split(',');
            return new TraceStep(cols[0], cols[1].Split(';').Select(item => JToken.FromObject(item)).ToArray<JToken>(), JToken.FromObject(cols[2]));
        }

        public override string ToString()
        {
            return this.methodName + "," + (this.args != null ? String.Join(";", args.Select(arg => arg.ToString())) : "") + "," + (this.result != null ? this.result.ToString() : "");
        }
    }

    public class TestingSession : ITestingService
    {
        // metadata
        public string id;
        public string assemblyName;
        public string assemblyPath;
        public string methodDeclaringClass;
        public string methodName;
        public int schedulingSeed;

        // run-time objects
        private StreamWriter traceFile;
        public StreamWriter logger;
        private Action<TestingSession> _onComplete;
        private object stateLock;
        private bool replayMode; // indicates it has already ran once
        private DateTime startedAt;
        private DateTime finishedAt;
        private TimeSpan elapsed;

        // testing service objects
        private Helpers.SeededRandomizer randomizer;
        private int counter;
        ProgramState programState;
        TaskCompletionSource<TestResult> IterFinished;
        int numPendingTaskCreations;
        Queue<TraceStep> testTrace;
        public bool finished;

        // result data
        public bool passed;
        public string reason;

        public TestingSession(string assemblyName, string assemblyPath, string methodDeclaringClass, string methodName, int schedulingSeed)
        {
            this.id = Helpers.RandomString(8);
            this.assemblyName = assemblyName;
            this.assemblyPath = assemblyPath;
            this.methodDeclaringClass = methodDeclaringClass;
            this.methodName = methodName;
            this.schedulingSeed = schedulingSeed;
            this.passed = false;
            this.reason = null;

            // initialize run-time objects
            this._onComplete = self => { };     // empty onComplete handler
            this.stateLock = new object();
            this.replayMode = false;
            this.traceFile = File.AppendText("logs/trace-" + this.id + ".csv");

            this.Reset();
        }

        public SessionInfo info { get { return new SessionInfo(this.id, this.assemblyName, this.assemblyPath, this.methodDeclaringClass, this.methodName, this.schedulingSeed); } }

        public bool IsFinished { get { return this.finished; } }

        public double ElapsedMilliseconds { get { return this.elapsed.TotalMilliseconds; } }
        public double ElapsedSeconds { get { return this.elapsed.TotalSeconds; } }

        public void OnComplete(Action<TestingSession> action)
        {
            this._onComplete = action;
        }

        public void Finish(bool passed, string reason)
        {
            this.IterFinished.SetResult(new TestResult(false, reason));
        }

        public void Reset()
        {
            // reset run-time objects
            this.randomizer = new Helpers.SeededRandomizer(this.schedulingSeed);
            this.counter = 0;
            this.programState = new ProgramState();
            this.IterFinished = new TaskCompletionSource<TestResult>();
            this.programState.taskToTcs.Add(0, new TaskCompletionSource<bool>());
            this.numPendingTaskCreations = 0;
            this.testTrace = new Queue<TraceStep>();
            this.finished = false;

            this.startedAt = DateTime.Now;

            // create a continuation callback that will notify the client once the test is finished
            this.IterFinished.Task.ContinueWith(prev => {
                
                this.finishedAt = DateTime.Now;
                this.elapsed = this.finishedAt - this.startedAt;

                this.passed = prev.Result.passed;
                this.reason = prev.Result.reason;

                Console.WriteLine("Trace Length: {0}", testTrace.Count);
                // write trace only if this is the first time running this session
                if (this.replayMode == false)
                {
                    this.traceFile.Write(String.Join("\n", this.testTrace.Select(step => step.ToString())));
                    this.traceFile.Close();
                }

                lock (this.stateLock)
                {
                    this.finished = true;
                    this.replayMode = true;
                }

                this._onComplete(this);
            });
        }

        private void PushTrace(string methodName, JToken[] args, JToken result)
        {
            lock (this.testTrace)
            {
                this.testTrace.Enqueue(new TraceStep(methodName, args, result));
            }
        }

        private void AppendLog(string line)
        {
            Console.WriteLine(line);
            this.logger.WriteLine(line);
        }

        private void AppendLog(params object[] cols)
        {
            Console.WriteLine(String.Join("\t", cols.Select(obj => obj.ToString())));
            this.logger.WriteLine(String.Join(",", cols.Select(obj => obj.ToString())));
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        public void CreateTask()
        {
            AppendLog(counter++, "CreateTask", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            lock (programState)
            {
                this.numPendingTaskCreations++;
            }

            this.PushTrace("CreateTask", null, null);
            AppendLog(counter++, "CreateTask", "", "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public void StartTask(int taskId)
        {
            AppendLog(counter++, "StartTask", taskId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lock (programState)
            {
                Assert(!programState.taskToTcs.ContainsKey(taskId), $"Duplicate declaration of task: {taskId}");
                programState.taskToTcs.Add(taskId, tcs);
                numPendingTaskCreations--;
                Console.WriteLine("{0}\tTask {1}\tcreated", counter, taskId);
            }

            tcs.Task.Wait();

            this.PushTrace("StartTask", new JToken[] { taskId }, null);
            AppendLog(counter++, "StartTask", taskId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public void EndTask(int taskId)
        {
            AppendLog(counter++, "EndTask", taskId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            WaitForPendingTaskCreations();

            lock (programState)
            {
                Assert(programState.taskToTcs.ContainsKey(taskId), $"EndTask called on unknown task: {taskId}");
                programState.taskToTcs.Remove(taskId);
            }

            ContextSwitch();

            this.PushTrace("EndTask", new JToken[] { taskId }, null);
            AppendLog(counter++, "EndTask", taskId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public void CreateResource(int resourceId)
        {
            AppendLog(counter++, "CreateResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            lock (programState)
            {
                Assert(!programState.resourceSet.Contains(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.resourceSet.Add(resourceId);
            }

            this.PushTrace("CreateResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "CreateResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public void DeleteResource(int resourceId)
        {
            AppendLog(counter++, "DeleteResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            lock (programState)
            {
                Assert(programState.resourceSet.Contains(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                programState.resourceSet.Remove(resourceId);
            }

            this.PushTrace("DeleteResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "DeleteResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public void BlockedOnResource(int resourceId)
        {
            AppendLog(counter++, "BlockedOnResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            lock (programState)
            {
                Assert(programState.resourceSet.Contains(resourceId),
                    $"Illegal operation, resource {resourceId} has not been declared");
                Assert(!programState.taskStatus.ContainsKey(programState.currentTask),
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.taskStatus[programState.currentTask] = resourceId;
            }

            ContextSwitch();

            this.PushTrace("BlockedOnResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "BlockedOnResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            AppendLog(counter++, "SignalUpdatedResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            lock (programState)
            {
                var enabledTasks = programState.taskStatus.Where(tup => tup.Value == resourceId).Select(tup => tup.Key).ToList();
                foreach (var k in enabledTasks)
                {
                    programState.taskStatus.Remove(k);
                }
            }

            this.PushTrace("SignalUpdatedResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "SignalUpdatedResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        public bool CreateNondetBool()
        {
            AppendLog(counter++, "CreateNondetBool", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);

            var value = this.randomizer.NextBool();

            this.PushTrace("CreateNondetBool", null, JToken.FromObject(value));
            return value;
        }

        public int CreateNondetInteger(int maxValue)
        {
            AppendLog(counter++, "CreateNondetInteger", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
            var value = this.randomizer.NextInt(maxValue);
            this.PushTrace("CreateNondetInteger", new JToken[] { maxValue }, JToken.FromObject(value));
            return value;
        }

        public void Assert(bool value, string message)
        {
            AppendLog(counter++, "Assert", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
            this.PushTrace("Assert", new JToken[] { value, message }, null);

            if (!value) throw new AssertionFailureException(message);
        }

        public void ContextSwitch()
        {
            AppendLog(counter++, "ContextSwitch", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
            this.PushTrace("ContextSwitch", null, null);

            WaitForPendingTaskCreations();

            var tcs = new TaskCompletionSource<bool>();
            List<int> enabledTasks;
            int next;
            int currentTask;
            bool currentTaskEnabled = false;

            lock (programState)
            {
                currentTask = programState.currentTask;
                currentTaskEnabled = programState.taskToTcs.ContainsKey(currentTask);

                // pick next one to execute
                enabledTasks = new List<int>(
                    programState.taskToTcs.Keys
                    .Where(k => !programState.taskStatus.ContainsKey(k))
                    );

                if (enabledTasks.Count == 0)
                {
                    // if all remaining tasks are blocked then it's deadlock
                    Assert(programState.taskToTcs.Count == 0, "Deadlock detected");

                    // all-done
                    IterFinished.SetResult(new TestResult(true));
                    return;
                }

                next = this.randomizer.NextInt(enabledTasks.Count);
            }

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
                    if (currentTaskEnabled)
                    {
                        // if current task is still running, save the new tcs
                        programState.taskToTcs[currentTask] = tcs;
                    }
                    // update the current task
                    programState.currentTask = enabledTasks[next];
                }

                // complete the tcs to let the task continue
                nextTcs.SetResult(true);

                if (currentTaskEnabled)
                {
                    // block the current task until it is resumed by a future contextswitch call
                    tcs.Task.Wait();
                }
            }

            AppendLog(counter++, "ContextSwitch", "", "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, programState.taskStatus.Count, programState.taskToTcs.Count);
        }

        void WaitForPendingTaskCreations()
        {
            // Console.WriteLine("{0}\tWaitForPendingTaskCreations()\tenter\t{1}/{2}", count, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);            
            // AppendLog(count, "WaitForPendingTaskCreations", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            // this.PushTrace("WaitForPendingTaskCreations", null, null);

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
    }
}
