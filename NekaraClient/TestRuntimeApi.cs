using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;
using System.Diagnostics;

namespace Nekara.Client
{
    public class TestRuntimeApi : ITestingService
    {
        private static Process currentProcess = Process.GetCurrentProcess();
        private static int PrintVerbosity = 0;
        public static Helpers.MicroProfiler Profiler = new Helpers.MicroProfiler();
        private static int gCount = 0;

        private object stateLock;
        public IClient socket;
        private HashSet<ApiRequest> pendingRequests;

        public string sessionId;
        private int count;
        private bool finished;

        // performance data
        public int numRequests;
        public double avgRtt;      // average round-trip time (time taken between sending of request and receiving of response)

        public TestRuntimeApi(IClient socket)
        {
            this.stateLock = new object();
            this.socket = socket;
            this.pendingRequests = new HashSet<ApiRequest>();

            this.sessionId = null;
            this.count = 0;
            this.finished = false;

            this.numRequests = 0;
            this.avgRtt = 0;
        }

        // Called by the parent object (TestingServiceProxy) to give 
        // control to the RuntimeAPI to stop the test
        public void SetSessionId(string sessionId)
        {
            this.sessionId = sessionId;
            this.count = 0;
            this.finished = false;

            this.numRequests = 0;
            this.avgRtt = 0;
        }

        public void Finish()
        {
            lock (this.stateLock)
            {
                this.finished = true;

                this.pendingRequests.ToList().ForEach(req => req.Cancel());
                var tasks = this.pendingRequests.Select(req => req.Task).ToArray();

                if (PrintVerbosity > 0)
                {
                    Console.WriteLine("\n\n    ... cleaning up {0} pending tasks", tasks.Length);
                    Console.WriteLine(String.Join("", this.pendingRequests.Select(req => "\n\t  ... " + req.Label)));
                }
                
                try
                {
                    Task.WaitAll(tasks);
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
            string callName = $"{func}({String.Join(",", args.Select(arg => arg.ToString()))})";
            DateTime sentAt;
            (string, long) stamp;

            ApiRequest request;

            lock (this.stateLock)
            {
                if (!this.finished)
                {
                    sentAt = DateTime.Now;
                    stamp = Profiler.Update(func + "Call");

                    var extargs = new JToken[args.Length + 1];
                    extargs[0] = JToken.FromObject(this.sessionId);
                    Array.Copy(args, 0, extargs, 1, args.Length);

                    if (PrintVerbosity > 0) Console.WriteLine($"{count++}\t{Thread.CurrentThread.ManagedThreadId}/{currentProcess.Threads.Count}\t--->>\t{func}({String.Join(", ", args.Select(arg => arg.ToString()).ToArray())})");
                    var (task, canceller) = this.socket.SendRequest(func, extargs);
                    request = new ApiRequest(task, canceller, callName);

                    this.pendingRequests.Add(request);
                }
                else throw new SessionAlreadyFinishedException($"Session already finished, suspending further requests: [{func}]");
            }

            try
            {
                request.Task.Wait();
                lock (this.stateLock)
                {
                    Interlocked.Exchange(ref this.avgRtt, ((DateTime.Now - sentAt).TotalMilliseconds + numRequests * avgRtt) / (numRequests + 1));
                    Interlocked.Increment(ref this.numRequests);
                    
                    stamp = Profiler.Update(func + "Return", stamp);

                    this.pendingRequests.Remove(request);

                    if (PrintVerbosity > 0) Console.WriteLine($"{count++}\t{Thread.CurrentThread.ManagedThreadId}/{currentProcess.Threads.Count}\t<<---\t{func}({String.Join(", ", args.Select(arg => arg.ToString()).ToArray())})");

                    if (this.finished) throw new SessionAlreadyFinishedException($"[{func}] returned but session already finished, throwing to prevent further progress");
                }
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("\n\n[TestRuntimeApi.InvokeAndHandleException] AggregateException/{1} caught during {0}!", func, aex.InnerException.GetType().Name);
                if (aex.InnerException is AssertionFailureException
                    || aex.InnerException is TestFailedException) throw new IntentionallyIgnoredException(aex.InnerException.Message, aex.InnerException);
                else throw;
            }
            finally
            {
                // this.pendingRequests.Remove((task, canceller));
            }
            return request.Task.Result;
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

        public void BlockedOnAnyResource(params int[] resourceIds)
        {
            InvokeAndHandleException("BlockedOnAnyResource", new JToken[] { new JArray(resourceIds.Select(id => JToken.FromObject(id))) });
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
            return InvokeAndHandleException("CreateNondetInteger", maxValue).ToObject<int>();
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
            var serializedResult = InvokeAndHandleException("WaitForMainTask").ToObject<string>();
            
            this.Finish();

            return serializedResult;
        }
    }
}