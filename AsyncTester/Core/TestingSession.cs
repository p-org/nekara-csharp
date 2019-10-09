using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace AsyncTester.Core
{
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
        public string methodName;
        public int schedulingSeed;

        private StreamWriter traceFile;
        public StreamWriter logger;

        // run-time objects
        private Helpers.SeededRandomizer randomizer;
        private int counter;
        private Action<TestingSession> _onComplete;
        ProgramState programState;
        TaskCompletionSource<TestResult> IterFinished;
        int numPendingTaskCreations;
        Queue<TraceStep> testTrace;

        // result data
        public bool passed;
        public string reason;

        public TestingSession(string assemblyName, string assemblyPath, string methodName, int schedulingSeed)
        {
            this.id = Helpers.RandomString(8);
            this.assemblyName = assemblyName;
            this.assemblyPath = assemblyPath;
            this.methodName = methodName;
            this.schedulingSeed = schedulingSeed;
            this.passed = false;
            this.reason = null;

            // initialize run-time objects
            this.randomizer = new Helpers.SeededRandomizer(schedulingSeed);
            this.counter = 0;
            this._onComplete = self => { };     // empty onComplete handler
            this.programState = new ProgramState();
            this.IterFinished = new TaskCompletionSource<TestResult>();
            this.programState.taskToTcs.Add(0, new TaskCompletionSource<bool>());
            this.numPendingTaskCreations = 0;
            this.testTrace = new Queue<TraceStep>();
            // this.isReplayMode = false;            

            string tracePath = "logs/trace-" + this.id + ".csv";
            this.traceFile = File.AppendText(tracePath);

            // create a continuation callback that will notify the client once the test is finished
            this.IterFinished.Task.ContinueWith(prev => {

                this.passed = prev.Result.passed;
                this.reason = prev.Result.reason;

                Console.WriteLine("Trace Length: {0}", testTrace.Count);
                this.traceFile.Write(String.Join("\n", this.testTrace.Select(step => step.ToString())));
                this.traceFile.Close();

                this._onComplete(this);
            });

        }

        public void OnComplete(Action<TestingSession> action)
        {
            this._onComplete = action;
        }

        public void Finish(bool passed, string reason)
        {
            this.IterFinished.SetResult(new TestResult(false, reason));
        }

        public void Replay()
        {
            // this.count = 0;

            // The following should really be managed per client session.
            // For now, we assume there is only 1 client.
            this.programState = new ProgramState();
            this.IterFinished = new TaskCompletionSource<TestResult>();
            this.programState.taskToTcs.Add(0, new TaskCompletionSource<bool>());
            this.numPendingTaskCreations = 0;
            // this.testTrace = new List<string>();
            // this.isReplayMode = true;

            // string tracePath = "logs/trace-" + this.sessionId + ".csv";
            // string[] trace = File.ReadAllText(tracePath).Split('\n');
            // this.testTrace = new Queue<TraceStep>(trace.Select(row => TraceStep.FromString(row)));

            // this.traceFile = File.AppendText(tracePath);
            // this.traceFile.WriteLine("Description");

            // create a continuation callback that will notify the client once the test is finished
            /*this.IterFinished.Task.ContinueWith(prev => {
                // notifyClient(this.sessionId);
                var client = this.server.GetClient();
                var message = new RequestMessage("Tester-Server", client.id, "FinishTest", new JToken[] { sessionId });
                var serialized = JsonConvert.SerializeObject(message);
                client.Send(serialized);

                Console.WriteLine("Test {0} Finished!", this.sessionId);
                // Make sure that the result is the same as before


                this.sessionId = null;      // Clear the sessionId so the next session can begin
            });

            return info;*/
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
            AppendLog(counter++, "CreateTask", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            lock (programState)
            {
                this.numPendingTaskCreations++;
            }

            this.PushTrace("CreateTask", null, null);
            AppendLog(counter++, "CreateTask", "", "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void StartTask(int taskId)
        {
            AppendLog(counter++, "StartTask", taskId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lock (programState)
            {
                Assert(!programState.taskToTcs.ContainsKey(taskId), $"Duplicate declaration of task: {taskId}");
                programState.taskToTcs.Add(taskId, tcs);
                numPendingTaskCreations--;
            }

            tcs.Task.Wait();

            this.PushTrace("StartTask", new JToken[] { taskId }, null);
            AppendLog(counter++, "StartTask", taskId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void EndTask(int taskId)
        {
            AppendLog(counter++, "EndTask", taskId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            WaitForPendingTaskCreations();

            lock (programState)
            {
                Assert(programState.taskToTcs.ContainsKey(taskId), $"EndTask called on unknown task: {taskId}");
                programState.taskToTcs.Remove(taskId);
            }

            ContextSwitch();

            this.PushTrace("EndTask", new JToken[] { taskId }, null);
            AppendLog(counter++, "EndTask", taskId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void CreateResource(int resourceId)
        {
            AppendLog(counter++, "CreateResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            lock (programState)
            {
                Assert(!programState.resourceSet.Contains(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.resourceSet.Add(resourceId);
            }

            this.PushTrace("CreateResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "CreateResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void DeleteResource(int resourceId)
        {
            AppendLog(counter++, "DeleteResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            lock (programState)
            {
                Assert(programState.resourceSet.Contains(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                programState.resourceSet.Remove(resourceId);
            }

            this.PushTrace("DeleteResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "DeleteResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void BlockedOnResource(int resourceId)
        {
            AppendLog(counter++, "BlockedOnResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            lock (programState)
            {
                Assert(!programState.taskStatus.ContainsKey(programState.currentTask),
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.taskStatus[programState.currentTask] = resourceId;
            }

            ContextSwitch();

            this.PushTrace("BlockedOnResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "BlockedOnResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            AppendLog(counter++, "SignalUpdatedResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            lock (programState)
            {
                var enabledTasks = programState.taskStatus.Where(tup => tup.Value == resourceId).Select(tup => tup.Key).ToList();
                foreach (var k in enabledTasks)
                {
                    programState.taskStatus.Remove(k);
                }
            }

            this.PushTrace("SignalUpdatedResource", new JToken[] { resourceId }, null);
            AppendLog(counter++, "SignalUpdatedResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public bool CreateNondetBool()
        {
            AppendLog(counter++, "CreateNondetBool", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

            var value = this.randomizer.NextBool();

            this.PushTrace("CreateNondetBool", null, JToken.FromObject(value));
            return value;
        }

        public int CreateNondetInteger(int maxValue)
        {
            AppendLog(counter++, "CreateNondetInteger", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var value = this.randomizer.NextInt(maxValue);
            this.PushTrace("CreateNondetInteger", new JToken[] { maxValue }, JToken.FromObject(value));
            return value;
        }

        public void Assert(bool value, string message)
        {
            AppendLog(counter++, "Assert", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.PushTrace("Assert", new JToken[] { value, message }, null);

            if (!value) throw new AssertionFailureException(message);
        }

        public void ContextSwitch()
        {
            AppendLog(counter++, "ContextSwitch", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
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
                TaskCompletionSource<bool> nextTcs;

                lock (programState)
                {
                    nextTcs = programState.taskToTcs[enabledTasks[next]];
                    if (currentTaskEnabled)
                    {
                        programState.taskToTcs[programState.currentTask] = tcs;
                    }
                    programState.currentTask = enabledTasks[next];
                }

                nextTcs.SetResult(true);

                if (currentTaskEnabled)
                {
                    tcs.Task.Wait();
                }
            }

            AppendLog(counter++, "ContextSwitch", "", "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
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
