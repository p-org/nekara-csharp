using Nekara.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using Nekara.Abstractions;
using System.ComponentModel;

namespace Nekara.Client
{
    public class ClientSession
    {
        public readonly string Id;
        public readonly IClient socket;
        public readonly int RunNumber;

        private object stateLock;
        private HashSet<ApiRequest> pendingRequests;
        public Helpers.UniqueIdGenerator TaskIdGenerator;
        public Helpers.UniqueIdGenerator ResourceIdGenerator;

        public Concurrent<bool> IsFinished;
        public int count;

        // performance data
        public int numRequests;
        public double avgRtt;      // average round-trip time (time taken between sending of request and receiving of response)

        public ClientSession(ref IClient socket, (string, int) sessionKey)
        {
            this.socket = socket;
            this.Id = sessionKey.Item1;
            this.RunNumber = sessionKey.Item2;
            this.stateLock = new object();
            this.pendingRequests = new HashSet<ApiRequest>();
            this.TaskIdGenerator = new Helpers.UniqueIdGenerator(true, 1000);
            this.ResourceIdGenerator = new Helpers.UniqueIdGenerator(true, 1000000);
            
            this.IsFinished = new Concurrent<bool>();

            this.count = 0;
            this.numRequests = 0;
            this.avgRtt = 0.0;
        }

        public void Finish()
        {
            lock (this.stateLock)
            {
                this.IsFinished.Value = true;

                this.pendingRequests.ToList().ForEach(req => req.Cancel());
                var tasks = this.pendingRequests.Select(req => req.Task).ToArray();

                if (RuntimeEnvironment.PrintVerbosity > 0)
                {
                    Console.WriteLine("\n\n    ... cleaning up {0} pending tasks", tasks.Length);
                    Console.WriteLine(String.Join("", this.pendingRequests.Select(req => $"\n\t  ... {req.Label}\t({req.Task.Status.ToString()})")));
                }

                try
                {
                    Task.WaitAll(tasks);
                    if (RuntimeEnvironment.PrintVerbosity > 0) Console.WriteLine("    ... successfully cleaned up {0} pending tasks", tasks.Length);
                    this.pendingRequests.Clear();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("    ... Ignoring {0} thrown from {1} pending tasks", ex.GetType().Name, tasks.Length);
                    Console.WriteLine(String.Join("", this.pendingRequests.Select(req => $"\n\t  ... {req.Label}\t({req.Task.Status.ToString()})")));
                    // Console.WriteLine(ex);
                    this.pendingRequests.Clear();
                }

                var allPending = Nekara.Models.Task.AllPending.ToArray();
                try
                {
                    Nekara.Models.Task.WaitAll(allPending);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("    ... Ignoring {0} thrown from ALL {1} pending tasks", ex.GetType().Name, allPending.Length);
                    Console.WriteLine(String.Join("", allPending.Select(item => $"\n\t  ... {item.Id}\t({item.ResourceId})")));
                }
            }
        }

        public JToken InvokeAndHandleException(string func, params JToken[] args)
        {
            string callName = $"Session {Id}.{RunNumber} / " + Helpers.MethodInvocationString(func, args);

            if (this.IsFinished.Value) throw new SessionAlreadyFinishedException($"Session already finished, suspending further requests: [{callName}]");

            long sentAt = Stopwatch.GetTimestamp();
            (string, long) stamp = TestRuntimeApi.Profiler.Update(func + "Call");

            var extargs = new JToken[args.Length + 1];
            extargs[0] = JToken.FromObject(this.Id);
            Array.Copy(args, 0, extargs, 1, args.Length);

            if (RuntimeEnvironment.PrintVerbosity > 1) Console.WriteLine($"{this.Id}: {count++}\t{Thread.CurrentThread.ManagedThreadId}/{TestRuntimeApi.currentProcess.Threads.Count}\t--->>\t{func}({String.Join(", ", args.Select(arg => arg.ToString()).ToArray())})");
            
            var (task, canceller) = this.socket.SendRequest(func, extargs);
            var request = new ApiRequest(task, canceller, callName);
            
            lock (this.pendingRequests)
            {
                this.pendingRequests.Add(request);
            }

            try
            {
                request.Task.Wait();

                Interlocked.Exchange(ref this.avgRtt, ((Stopwatch.GetTimestamp() - sentAt) / 10000 + numRequests * avgRtt) / (numRequests + 1));
                Interlocked.Increment(ref this.numRequests);

                stamp = TestRuntimeApi.Profiler.Update(func + "Return", stamp);

                lock (this.pendingRequests)
                {
                    this.pendingRequests.Remove(request);
                }

                if (RuntimeEnvironment.PrintVerbosity > 1) Console.WriteLine($"{this.Id}: {count++}\t{Thread.CurrentThread.ManagedThreadId}/{TestRuntimeApi.currentProcess.Threads.Count}\t<<---\t{func}({String.Join(", ", args.Select(arg => arg.ToString()).ToArray())})");

                if (this.IsFinished.Value) throw new SessionAlreadyFinishedException($"[{callName}] returned but session already finished, throwing to prevent further progress");
            }
            catch (AggregateException aex)    // We have to catch the exception here because any exception thrown from the function is (possibly) swallowed by the user program
            {
                Console.WriteLine("\n[ClientSession[{2}-{3}].InvokeAndHandleException]\n  {0}\tAggregateException/{1} caught!", callName, aex.InnerException.GetType().Name, this.Id, this.RunNumber);
                if (aex.InnerException is TestingServiceException)
                {
                    throw new IntentionallyIgnoredException(aex.InnerException.Message, aex.InnerException, new StackTrace(true));
                }
                else throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n!!! [ClientSession.InvokeAndHandleException] UNEXPECTED EXCEPTION {0} !!!", ex.GetType().Name);
                throw;
            }
            /*finally
            {
                // this.pendingRequests.Remove((task, canceller));
            }*/
            return request.Task.Result;
        }
    }
}
