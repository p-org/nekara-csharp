﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;

namespace Nekara.Client
{
    public class TestRuntimeApi : ITestingService
    {
        private static int gCount = 0;

        private object stateLock;
        public IClient socket;
        private HashSet<(Task,CancellationTokenSource)> pendingRequests;

        public string sessionId;
        private int count;
        private bool finished;

        public TestRuntimeApi(IClient socket)
        {
            this.stateLock = new object();
            this.socket = socket;
            this.pendingRequests = new HashSet<(Task,CancellationTokenSource)>();

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

        public void Finish()
        {
            lock (this.stateLock)
            {
                this.finished = true;

                this.pendingRequests.ToList().ForEach(tup => tup.Item2.Cancel());
                var tasks = this.pendingRequests.Select(tuple => tuple.Item1).ToArray();
                Console.WriteLine("\n\n    ... cleaning up {0} pending tasks", tasks.Length);

                var pending = Task.WhenAll(tasks);
                
                try
                {
                    pending.Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("    ... Ignoring {0} thrown from {1} pending tasks", ex.GetType().Name, tasks.Length);
                    //Console.WriteLine(ex.Message);
                }
                finally
                {
                    this.pendingRequests.Clear();
                }
            }
        }

        private JToken InvokeAndHandleException(string func, params JToken[] args)
        {
            Task<JToken> task = null;
            CancellationTokenSource canceller = null;
            lock (this.stateLock)
            {
                if (!this.finished)
                {
                    var extargs = new JToken[args.Length + 1];
                    extargs[0] = JToken.FromObject(this.sessionId);
                    Array.Copy(args, 0, extargs, 1, args.Length);

                    Console.WriteLine($"{count++}\t{Thread.CurrentThread.ManagedThreadId}/{Process.GetCurrentProcess().Threads.Count}\t--->>\t{func}({String.Join(", ", args.Select(arg => arg.ToString()).ToArray())})");
                    (task, canceller) = this.socket.SendRequest(func, extargs);

                    this.pendingRequests.Add((task, canceller));
                }
                else throw new SessionAlreadyFinishedException($"Session already finished, suspending further requests: [{func}]");
            }

            try
            {
                task.Wait();
                lock (this.stateLock)
                {
                    this.pendingRequests.Remove((task, canceller));
                    Console.WriteLine($"{count++}\t{Thread.CurrentThread.ManagedThreadId}/{Process.GetCurrentProcess().Threads.Count}\t<<---\t{func}({String.Join(", ", args.Select(arg => arg.ToString()).ToArray())})");

                    if (this.finished) throw new SessionAlreadyFinishedException($"[{func}] returned but session already finished, throwing to prevent further progress");
                }
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("\n\n[TestRuntimeApi.InvokeAndHandleException] AggregateException caught during {0}!", func);
                if (aex.InnerException is AssertionFailureException
                    || aex.InnerException is TestFailedException) throw new IntentionallyIgnoredException(aex.InnerException.Message, aex.InnerException);
                else throw;
            }
            finally
            {
                // this.pendingRequests.Remove((task, canceller));
            }
            return task.Result;
        }

        /* API methods */
        public void CreateTask()
        {
            InvokeAndHandleException("CreateTask");
        }

        public void StartTask(int taskId)
        {
            InvokeAndHandleException("StartTask", taskId);
        }

        public void EndTask(int taskId)
        {
            InvokeAndHandleException("EndTask", taskId);
        }

        public void CreateResource(int resourceId)
        {
            InvokeAndHandleException("CreateResource", resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            InvokeAndHandleException("DeleteResource", resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            InvokeAndHandleException("BlockedOnResource", resourceId);
        }

        public void SignalUpdatedResource(int resourceId)
        {
            InvokeAndHandleException("SignalUpdatedResource", resourceId);
        }

        public bool CreateNondetBool()
        {
            return InvokeAndHandleException("CreateNondetBool").ToObject<bool>();
        }

        public int CreateNondetInteger(int maxValue)
        {
            return InvokeAndHandleException("CreateNondetInteger").ToObject<int>();
        }

        public void Assert(bool predicate, string s)
        {
            InvokeAndHandleException("Assert", predicate, s);
        }

        public void ContextSwitch()
        {
            InvokeAndHandleException("ContextSwitch");
        }

        public string WaitForMainTask()
        {
            var reason = InvokeAndHandleException("WaitForMainTask").ToObject<string>();
            
            this.Finish();
            
            return reason;
        }
    }
}