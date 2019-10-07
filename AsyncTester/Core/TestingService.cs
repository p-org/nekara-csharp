using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PSharp;
using Microsoft.PSharp.TestingServices;
using Grpc.Core.Logging;
using AsyncTester.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using Grpc.Core;

namespace AsyncTester.Core
{
    /* The objects below are transport-agnostic and deals only with the user-facing testing API.
     * The only thing related to the transport mechanism is the RemoteMethodAttribute
     */

    // This is the service object exposed to the client, and hosted on the server-side
    // The API should be defined on this object.
    public class TestingService : MarshalByRefObject, ITestingService
    {
        private struct TestResult
        {
            public bool passed;
            public string sessionId;
            public string reason;
            public TestResult(bool passed, string sessionId, string reason = "") {
                this.passed = passed;
                this.sessionId = sessionId;
                this.reason = reason;
            }
        }

        public struct SessionInfo
        {
            public string id;
            public string assemblyName;
            public string assemblyPath;
            public string methodName;

            public SessionInfo(string id, string assemblyName, string assemblyPath, string methodName)
            {
                this.id = id;
                this.assemblyName = assemblyName;
                this.assemblyPath = assemblyPath;
                this.methodName = methodName;
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

        public static int gCount = 0;

        private Dictionary<string, TestResult> testResults;
        private Dictionary<string, SessionInfo> testSessions;

        private StreamWriter logFile;
        private StreamWriter traceFile;
        private StreamWriter summaryFile;
        private OmniServer server;      // keeping this reference is a temporary workaround to handle the InitializeTestSession notifyClient callback.
                                        // TODO: it should be handled more gracefully by revising the RemoteMethodAsync signature to accept a reference to ClientHandle
                                        //       and the respective reply/reject callbacks

        string sessionId;   // analogous to topLevelMachineId - used to identify the top-level test session object
        public int count;
        ProgramState programState;
        TaskCompletionSource<TestResult> IterFinished;
        int numPendingTaskCreations;
        Queue<TraceStep> testTrace;
        bool isReplayMode;

        public TestingService(OmniServer server)
        {
            this.testResults = new Dictionary<string, TestResult>();
            this.testSessions = new Dictionary<string, SessionInfo>();

            string logPath = "logs/log-" + DateTime.Now.Ticks.ToString() + ".csv";
            this.logFile = File.AppendText(logPath);
            this.logFile.WriteLine("Counter,Method,Arguments,Tag,Thread,NumThreads");

            string sumPath = "logs/summary-" + DateTime.Now.Ticks.ToString() + ".csv";
            this.summaryFile = File.AppendText(sumPath);
            this.summaryFile.WriteLine("SessionId,Result,Reason");

            this.server = server;

            this.sessionId = null;
            this.count = 0;
            this.programState = null;
            this.IterFinished = null;
            this.testTrace = null;
            this.traceFile = null;
            this.isReplayMode = false;
        }

        private void AppendLog(string line)
        {
            Console.WriteLine(line);
            this.logFile.WriteLine(line);
        }

        private void AppendLog(params object[] cols)
        {
            Console.WriteLine(String.Join("\t", cols.Select(obj => obj.ToString())));
            this.logFile.WriteLine(String.Join(",", cols.Select(obj => obj.ToString())));
        }

        private void PushTrace(string methodName, JToken[] args, JToken result)
        {
            lock (this.testTrace)
            {
                this.testTrace.Enqueue(new TraceStep(methodName, args, result));
            }
        }

        [RemoteMethod(name = "InitializeTestSession", description = "Initializes server-side proxy program that will represent the actual program on the client-side")]
        // treating this method as a special case because it spawns another Task we have to resolve later
        public string InitializeTestSession(JToken arg0, JToken arg1, JToken arg2)
        {
            // HACK: Wait till previous session finishes
            // - a better way to deal with this is to keep a dictionary of sessions
            // and associate each request with a particular session so that the sessions are isolated
            while (this.sessionId != null)
            {
                Thread.Sleep(100);
            }

            string assemblyName = arg0.ToObject<string>();
            string assemblyPath = arg1.ToObject<string>();
            string methodName = arg2.ToObject<string>();

            Console.WriteLine("Initializing test for [{1}] in {0}", assemblyName, methodName);

            this.sessionId = Helpers.RandomString(16);
            this.count = 0;
            this.testSessions.Add(this.sessionId, new SessionInfo(this.sessionId, assemblyName, assemblyPath, methodName));

            // The following should really be managed per client session.
            // For now, we assume there is only 1 client.
            this.programState = new ProgramState();
            this.IterFinished = new TaskCompletionSource<TestResult>();
            this.programState.taskToTcs.Add(0, new TaskCompletionSource<bool>());
            this.numPendingTaskCreations = 0;
            this.testTrace = new Queue<TraceStep>();
            this.isReplayMode = false;

            string tracePath = "logs/trace-" + this.sessionId + ".csv";
            this.traceFile = File.AppendText(tracePath);
            // this.traceFile.WriteLine("Description");

            // create a continuation callback that will notify the client once the test is finished
            this.IterFinished.Task.ContinueWith(prev => {
                // notifyClient(this.sessionId);
                var client = this.server.GetClient();   // HACK - this always returns the same client; should be updated to load client by session ID
                var message = new RequestMessage("Tester-Server", client.id, "FinishTest", new JToken[] { sessionId });
                var serialized = JsonConvert.SerializeObject(message);
                client.Send(serialized);

                Console.WriteLine("Test {0} Finished!", this.sessionId);

                this.testResults.Add(this.sessionId, prev.Result);

                // Flush the trace into a file

                /*if (prev.Result.passed) this.PushTrace("TEST PASSED");
                else this.PushTrace("TEST FAILED");*/

                Console.WriteLine("Trace Length: {0}", testTrace.Count);
                this.traceFile.Write(String.Join("\n", this.testTrace.Select(step => step.ToString())));
                this.traceFile.Close();

                // Append Summary
                string summary = prev.Result.sessionId + "," + (prev.Result.passed ? "pass" : "fail") + "," + prev.Result.reason;
                Console.WriteLine(summary);
                this.summaryFile.WriteLine(summary);
                this.summaryFile.Flush();

                Console.WriteLine("Results: {0}/{1}", this.testResults.Where(item => item.Value.passed == true).Count(), this.testResults.Count);

                this.sessionId = null;      // Clear the sessionId so the next session can begin
            });

            return sessionId;
        }

        [RemoteMethod(name = "AcknowledgeServerThrownException", description = "")]
        public void AcknowledgeServerThrownException(JToken message)
        {
            this.IterFinished.SetResult(new TestResult(false, this.sessionId, message.ToObject<string>()));
        }

        [RemoteMethod(name = "ReplayTestSession", description = "Replays the test session identified by the given session ID")]
        public SessionInfo ReplayTestSession(JToken arg)
        {
            // HACK: Wait till previous session finishes
            // - a better way to deal with this is to keep a dictionary of sessions
            // and associate each request with a particular session so that the sessions are isolated
            while (this.sessionId != null)
            {
                Thread.Sleep(100);
            }

            string sessionId = arg.ToObject<string>();

            SessionInfo info = this.testSessions[sessionId];
            Console.WriteLine("Replaying test {0}: [{2}] in {1}", sessionId, info.assemblyName, info.methodName);

            this.sessionId = sessionId;
            this.count = 0;

            // The following should really be managed per client session.
            // For now, we assume there is only 1 client.
            this.programState = new ProgramState();
            this.IterFinished = new TaskCompletionSource<TestResult>();
            this.programState.taskToTcs.Add(0, new TaskCompletionSource<bool>());
            this.numPendingTaskCreations = 0;
            // this.testTrace = new List<string>();
            this.isReplayMode = true;

            string tracePath = "logs/trace-" + this.sessionId + ".csv";
            string[] trace = File.ReadAllText(tracePath).Split('\n');
            this.testTrace = new Queue<TraceStep>(trace.Select(row => TraceStep.FromString(row)));

            // this.traceFile = File.AppendText(tracePath);
            // this.traceFile.WriteLine("Description");

            // create a continuation callback that will notify the client once the test is finished
            this.IterFinished.Task.ContinueWith(prev => {
                // notifyClient(this.sessionId);
                var client = this.server.GetClient();
                var message = new RequestMessage("Tester-Server", client.id, "FinishTest", new JToken[] { sessionId });
                var serialized = JsonConvert.SerializeObject(message);
                client.Send(serialized);

                Console.WriteLine("Test {0} Finished!", this.sessionId);
                // Make sure that the result is the same as before


                this.sessionId = null;      // Clear the sessionId so the next session can begin
            });

            return info;
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        [RemoteMethod(name = "CreateTask", description = "Creates a new task")]
        public void CreateTask()
        {
            AppendLog(count++, "CreateTask", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            lock (programState)
            {
                this.numPendingTaskCreations++;
            }
            this.PushTrace("CreateTask", null, null);
            AppendLog(count++, "CreateTask", "", "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        [RemoteMethod(name = "StartTask", description = "Signals the start of a given task")]
        public void StartTask(JToken taskId)
        {
            AppendLog(count++, "StartTask", taskId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.StartTask(taskId.ToObject<int>());
            this.PushTrace("StartTask", new JToken[] { taskId }, null);
            AppendLog(count++, "StartTask", taskId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
        public void StartTask(int taskId)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            lock (programState)
            {
                Assert(!programState.taskToTcs.ContainsKey(taskId), $"Duplicate declaration of task: {taskId}");
                programState.taskToTcs.Add(taskId, tcs);
                numPendingTaskCreations--;
            }

            tcs.Task.Wait();
        }

        [RemoteMethod(name = "EndTask", description = "Signals the end of a given task")]
        public void EndTask(JToken taskId)
        {
            AppendLog(count++, "EndTask", taskId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.EndTask(taskId.ToObject<int>());
            this.PushTrace("EndTask", new JToken[] { taskId }, null);
            AppendLog(count++, "EndTask", taskId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
        public void EndTask(int taskId)
        {
            WaitForPendingTaskCreations();

            lock (programState)
            {
                Assert(programState.taskToTcs.ContainsKey(taskId), $"EndTask called on unknown task: {taskId}");
                programState.taskToTcs.Remove(taskId);
            }

            ContextSwitch();
        }

        [RemoteMethod(name = "CreateResource", description = "Notifies the creation of a new resource")]
        public void CreateResource(JToken resourceId)
        {
            AppendLog(count++, "CreateResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.CreateResource(resourceId.ToObject<int>());
            this.PushTrace("CreateResource", new JToken[] { resourceId }, null);
            AppendLog(count++, "CreateResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
        public void CreateResource(int resourceId)
        {
            lock (programState)
            {
                Assert(!programState.resourceSet.Contains(resourceId), $"Duplicate declaration of resource: {resourceId}");
                programState.resourceSet.Add(resourceId);
            }
        }

        [RemoteMethod(name = "DeleteResource", description = "Signals the deletion of a given resource")]
        public void DeleteResource(JToken resourceId)
        {
            AppendLog(count++, "DeleteResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.DeleteResource(resourceId.ToObject<int>());
            this.PushTrace("DeleteResource", new JToken[] { resourceId }, null);
            AppendLog(count++, "DeleteResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
        public void DeleteResource(int resourceId)
        {
            lock (programState)
            {
                Assert(programState.resourceSet.Contains(resourceId), $"DeleteResource called on unknown resource: {resourceId}");
                programState.resourceSet.Remove(resourceId);
            }
        }

        [RemoteMethod(name = "BlockedOnResource", description = "")]
        public void BlockedOnResource(JToken resourceId)
        {
            AppendLog(count++, "BlockedOnResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.BlockedOnResource(resourceId.ToObject<int>());
            this.PushTrace("BlockedOnResource", new JToken[] { resourceId }, null);
            AppendLog(count++, "BlockedOnResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
        public void BlockedOnResource(int resourceId)
        {
            lock (programState)
            {
                Assert(!programState.taskStatus.ContainsKey(programState.currentTask),
                    $"Illegal operation, task {programState.currentTask} already blocked on a resource");
                programState.taskStatus[programState.currentTask] = resourceId;
            }

            ContextSwitch();
        }

        [RemoteMethod(name = "SignalUpdatedResource", description = "")]
        public void SignalUpdatedResource(JToken resourceId)
        {
            AppendLog(count++, "SignalUpdatedResource", resourceId, "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.SignalUpdatedResource(resourceId.ToObject<int>());
            this.PushTrace("SignalUpdatedResource", new JToken[] { resourceId }, null);
            AppendLog(count++, "SignalUpdatedResource", resourceId, "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
        public void SignalUpdatedResource(int resourceId)
        {
            lock (programState)
            {
                var enabledTasks = programState.taskStatus.Where(tup => tup.Value == resourceId).Select(tup => tup.Key).ToList();
                foreach (var k in enabledTasks)
                {
                    programState.taskStatus.Remove(k);
                }
            }
        }

        [RemoteMethod(name = "CreateNondetBool", description = "")]
        public bool CreateNondetBool()
        {
            AppendLog(count++, "CreateNondetBool", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var value = Helpers.RandomBool();
            this.PushTrace("CreateNondetBool", null, JToken.FromObject(value));
            return value;
        }

        [RemoteMethod(name = "CreateNondetInteger", description = "")]
        public int CreateNondetInteger(JToken maxValue)
        {
            AppendLog(count++, "CreateNondetInteger", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var value = this.CreateNondetInteger(maxValue.ToObject<int>());
            this.PushTrace("CreateNondetInteger", new JToken[] { maxValue }, JToken.FromObject(value));
            return value;
        }
        public int CreateNondetInteger(int maxValue)
        {
            return Helpers.RandomInt(maxValue);
        }

        [RemoteMethod(name = "Assert", description = "")]
        public void Assert(JToken value, JToken message)
        {
            AppendLog(count++, "Assert", "", "", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            this.PushTrace("Assert", new JToken[] { value, message }, null);
            this.Assert(value.ToObject<bool>(), message.ToObject<string>());
        }
        public void Assert(bool value, string message)
        {
            if (!value) throw new AssertionFailureException(message);
        }

        [RemoteMethod(name = "ContextSwitch", description = "Signals the deletion of a given resource")]
        public void ContextSwitch()
        {
            AppendLog(count++, "ContextSwitch", "", "enter", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
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
                    IterFinished.SetResult(new TestResult(true, this.sessionId));
                    return;
                }

                next = Helpers.RandomInt(enabledTasks.Count);
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
            AppendLog(count++, "ContextSwitch", "", "exit", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
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
