using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using AsyncTester.Networking;
using AsyncTester.Core;

namespace AsyncTester.Client
{
    public class TestRuntimeAPI : ITestingService
    {
        private static int gCount = 0;

        private object stateLock;
        public IClient socket;
        private HashSet<CancellationTokenSource> pendingTasks;

        public string sessionId;
        private int count;
        private bool finished;

        public TestRuntimeAPI(IClient socket)
        {
            this.stateLock = new object();
            this.socket = socket;
            this.pendingTasks = new HashSet<CancellationTokenSource>();

            this.sessionId = null;
            this.count = 0;
            this.finished = false;
        }

        // Called by the parent object (TestingServiceProxy) to give 
        // control to the RuntimeAPI to stop the test
        public void SetSessionId(string sessionId)
        {
            this.sessionId = sessionId;
            this.count = 0;
            this.finished = false;
        }

        private JToken InvokeAndHandleException(Func<(Task<JToken>, CancellationTokenSource)> action, string func = "Anonymous Function")
        {
            Task<JToken> task = null;
            CancellationTokenSource canceller = null;
            lock (this.stateLock)
            {
                if (!this.finished)
                {
                    (task, canceller) = action();
                    this.pendingTasks.Add(canceller);
                }
                else throw new SessionAlreadyFinishedException("Session already finished, suspending further requests");
            }

            try
            {
                task.Wait();
                this.pendingTasks.Remove(canceller);
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("AggregateException caught during {0}!", func);
                aex.Handle(ex => {
                    if (ex is TestingServiceException)
                    {
                        Console.WriteLine("    !!! Exception during test: {0}", ex.Message);
                        Console.WriteLine("    !!! Cancelling {0} requests....", this.pendingTasks.Count);
                        lock (this.stateLock)
                        {
                            this.finished = true;
                            foreach (var cts in this.pendingTasks)
                            {
                                cts.Cancel();
                            }
                            this.pendingTasks.Clear();
                        }
                        this.AcknowledgeServerThrownException(ex.Message);
                        return true;
                    }
                    else if (ex is LogisticalException)
                    {
                        Console.WriteLine(ex.Message);
                        return false;
                    }
                    else if (ex is RequestTimeoutException)
                    {
                        Console.WriteLine("    !!! Request Timed Out for method: {0}", func);
                        return false;
                    }
                    else
                    {
                        Console.WriteLine(ex);
                        return false;
                    }
                });
            }
            return task.Result;
        }

        public void AcknowledgeServerThrownException(string message)
        {
            Console.WriteLine("{0}\tAcknowledgeServerThrownException()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var (task, canceller) = this.socket.SendRequest("AcknowledgeServerThrownException", this.sessionId, message);
            task.Wait();
            Console.WriteLine("{0}\tAcknowledgeServerThrownException()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        /* API methods */
        public void CreateTask()
        {
            Console.WriteLine("{0}\tCreateTask()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("CreateTask", this.sessionId), "CreateTask");
            Console.WriteLine("{0}\tCreateTask()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);

        }

        public void StartTask(int taskId)
        {
            Console.WriteLine("{0}\tStartTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            InvokeAndHandleException(() => this.socket.SendRequest("StartTask", this.sessionId, taskId), "StartTask");
            Console.WriteLine("{0}\tStartTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public void EndTask(int taskId)
        {
            Console.WriteLine("{0}\tEndTask({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
            InvokeAndHandleException(() => this.socket.SendRequest("EndTask", this.sessionId, taskId), "ContextSwitch");
            Console.WriteLine("{0}\tEndTask({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, taskId);
        }

        public void CreateResource(int resourceId)
        {
            Console.WriteLine("{0}\tCreateResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("CreateResource", this.sessionId, resourceId), "CreateResource");
            Console.WriteLine("{0}\tCreateResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            Console.WriteLine("{0}\tDeleteResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("DeleteResource", this.sessionId, resourceId), "DeleteResource");
            Console.WriteLine("{0}\tDeleteResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            Console.WriteLine("{0}\tBlockedOnResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("BlockedOnResource", this.sessionId, resourceId), "BlockedOnResource");
            Console.WriteLine("{0}\tBlockedOnResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
            InvokeAndHandleException(() => this.socket.SendRequest("SignalUpdatedResource", this.sessionId, resourceId), "SignalUpdatedResource");
            Console.WriteLine("{0}\tSignalUpdatedResource({3})\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count, resourceId);
        }

        public bool CreateNondetBool()
        {
            Console.WriteLine("CreateNondetBool\t{0} / {1}", Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var value = InvokeAndHandleException(() => this.socket.SendRequest("CreateNondetBool", this.sessionId), "CreateNondetBool");
            return value.ToObject<bool>();
        }

        public int CreateNondetInteger(int maxValue)
        {
            Console.WriteLine("{0}\tCreateNondetInteger()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            var value = InvokeAndHandleException(() => this.socket.SendRequest("CreateNondetInteger", this.sessionId), "CreateNondetInteger");
            return value.ToObject<int>();
        }

        public void Assert(bool predicate, string s)
        {
            Console.WriteLine("{0}\tAssert()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("Assert", this.sessionId, predicate, s), "Assert");
            Console.WriteLine("{0}\tAssert()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }

        public void ContextSwitch()
        {
            Console.WriteLine("{0}\tContextSwitch()\tenter\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
            InvokeAndHandleException(() => this.socket.SendRequest("ContextSwitch", this.sessionId), "ContextSwitch");
            Console.WriteLine("{0}\tContextSwitch()\texit\t{1}/{2}", count++, Thread.CurrentThread.ManagedThreadId, Process.GetCurrentProcess().Threads.Count);
        }
    }
}
