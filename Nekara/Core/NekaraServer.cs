﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using System.Diagnostics;

/* The objects below are transport-agnostic and deals only with the user-facing testing API.
* The only thing related to the transport mechanism is the RemoteMethodAttribute
*/

namespace Nekara.Core
{
    /// <summary>
    /// This object maintains a set of <see cref="TestingSession"/>s, each representing a single run of a program under test.
    /// The methods annotated with <see cref="RemoteMethodAttribute"/> are exposed to the client via the network.
    /// </summary>
    public class NekaraServer : MarshalByRefObject
    {

        [System.Runtime.InteropServices.DllImport("NekaraCore.dll")]
        public static extern void NS_WithoutSeed(int max_decisions);
        [System.Runtime.InteropServices.DllImport("NekaraCore.dll")]
        public static extern void NS_WithSeed(int _seed, int max_decisions);

        public static decimal StartedAt = Math.Round((decimal)Stopwatch.GetTimestamp()/10000, 0);
        public static int gCount = 0;
        private Dictionary<string, TestingSession> testSessions;
        private StreamWriter summaryFile;

        public NekaraServer()
        {
            this.testSessions = new Dictionary<string, TestingSession>();

            this.summaryFile = File.AppendText("logs/summary-" + DateTime.Now.Ticks.ToString() + ".csv");
            this.summaryFile.WriteLine("Assembly,Class,Method,SessionId,Seed,Result,Reason,Elapsed");
        }

        /// <summary>
        /// This remote method is called by the client before the test run to exchange some metadata about the test.
        /// In particular, the client sends the scheduling seed to use for the run, along with some informational data
        /// such as the name of the Assembly, DeclaringType, and Method.
        /// This object creates a <see cref="TestingSession"/> object accordingly and returns the session ID to the client.
        /// The client must include the session ID as the first argument for every Nekara API method call, 
        /// so that the <see cref="NekaraServer"/> can route the request to the appropriate <see cref="TestingSession"/>.
        /// </summary>
        /// <param name="arg">Metadata and parameters for the test run, serialized as a JSON object. See <see cref="SessionInfo"/> for the format.</param>
        /// <returns></returns>
        [RemoteMethod(name = "InitializeTestSession", description = "Initializes server-side proxy program that will represent the actual program on the client-side")]
        public string InitializeTestSession(JObject arg)
        {
            var sessionInfo = SessionInfo.FromJson(arg);

            // New Changes:
            NS_WithSeed(sessionInfo.schedulingSeed, sessionInfo.maxDecisions);

            var session = new TestingSession(sessionInfo);

            session.OnComplete += (sender, record) =>
            {
#if DEBUG
                Console.WriteLine("\n==========[ Test {0} {1} ]==========\n", record.sessionId, record.passed ? "PASSED" : "FAILED");

                if (record.reason != "")
                {
                    Console.WriteLine("  " + record.reason);
                }
#endif

                // Append Summary
                string summary = String.Join(",", new string[] { 
                    sessionInfo.assemblyName, 
                    sessionInfo.methodDeclaringClass, 
                    sessionInfo.methodName, 
                    session.Id,
                    sessionInfo.schedulingSeed.ToString(),
                    (record.passed ? "pass" : "fail"),
                    record.reason,
                    record.elapsedMs.ToString() });
                this.summaryFile.WriteLine(summary);
                this.summaryFile.Flush();

#if DEBUG
                Console.WriteLine("\n--------------------------------------------\n");
                Console.WriteLine("    Total Requests:\t{0}", record.numRequests);
                Console.WriteLine("    Avg Invoke Time:\t{0} ms", Math.Round((decimal)record.avgInvokeTime, 2));
                Console.WriteLine("    Total Time Taken:\t{0} ms", Math.Round((decimal)record.elapsedMs, 2));
                Console.WriteLine("\n===== END of {0} =====[ Results: {1}/{2} ]=====\n\n", session.Id, this.testSessions.Where(item => item.Value.LastRecord.passed == true).Count(), this.testSessions.Count);
#endif
            };

            lock (this.testSessions)
            {
                this.testSessions.Add(session.Id, session);
            }
#if DEBUG
            Console.WriteLine("\n\n===== BEGIN {0} ================================\n", session.Id);
            Console.WriteLine("  [{1}]\n  in {0}", sessionInfo.assemblyName, sessionInfo.methodDeclaringClass + "." + sessionInfo.methodName);
            Console.WriteLine("  Seed:\t\t{0}\n  Timeout:\t{1}\n  MaxDecisions:\t{2}\n", sessionInfo.schedulingSeed, sessionInfo.timeoutMs, sessionInfo.maxDecisions);
            Console.WriteLine("\nIndex\tThrd\t#Thrds\tCurTask\t#Tasks\t#Blckd\tPending\tStage\tMethod\tArgs");
#else
            Console.WriteLine("\nRun new session '{0}'\tseed = {1}", session.Id, sessionInfo.schedulingSeed);
#endif

            return session.Id;
        }

        /// <summary>
        /// Similar to the <see cref="InitializeTestSession(JObject)"/> method, except this method
        /// accepts a session Id as the only argument. The server will look up the Dictionary of sessions,
        /// and return the metadata about the <see cref="TestingSession"/> if the session exists.
        /// </summary>
        /// <param name="arg">The session ID string</param>
        /// <returns><see cref="SessionInfo"/> containing the test session metadata</returns>
        [RemoteMethod(name = "ReplayTestSession", description = "Replays the test session identified by the given session ID")]
        public SessionInfo ReplayTestSession(JToken arg)
        {
            string sessionId = arg.ToObject<string>();

            SessionInfo info = GetSessionInfo(sessionId);
            lock (this.testSessions)
            {
                TestingSession session = this.testSessions[sessionId];
                session.Reset();

#if DEBUG
                Console.WriteLine("\n\n===== BEGIN REPLAY {0} =========================\n", sessionId);
                Console.WriteLine("  [{1}]\n  in {0}\n", info.assemblyName, info.methodDeclaringClass + "." + info.methodName);
                Console.WriteLine("  Seed:\t\t{0}\n  Timeout:\t{1}\n  MaxDecisions:\t{2}\n", info.schedulingSeed, info.timeoutMs, info.maxDecisions);
#else
                Console.WriteLine("\n Replay session '{0}'\tseed = {1}", session.Id, session.Meta.schedulingSeed);
#endif
            }

            return info;
        }

        [RemoteMethod(name = "GetSessionInfo", description = "Gets the Session info based on the session ID")]
        public SessionInfo GetSessionInfo(JToken arg)
        {
            string sessionId = arg.ToObject<string>();
            lock (this.testSessions)
            {
                if (!this.testSessions.ContainsKey(sessionId)) throw new SessionRecordNotFoundException("Session " + sessionId + " not found");
                return this.testSessions[sessionId].Meta;
            }
        }

        /// <summary>
        /// Routes the remote method call to the appropriate <see cref="TestingSession"/> object
        /// based on the session ID given as the first argument.
        /// </summary>
        /// <param name="sessionId">Session ID string</param>
        /// <param name="methodName">Name of the API method to call</param>
        /// <param name="args">Variadic arguments to pass to the method call</param>
        /// <returns>return value of the remote method</returns>
        private object RouteRemoteCall(string sessionId, string methodName, params object[] args)
        {
            return this.testSessions[sessionId].InvokeAndHandleException(methodName, args);
        }

        /* Methods below are the actual methods called (remotely) by the client-side proxy object.
         * Some methods have to be wrapped by an overloaded method because RemoteMethods are
         * expected to have a specific signature - i.e., all arguments are given as JTokens
         */
        [RemoteMethod(name = "CreateTask", description = "Creates a new task")]
        public void CreateTask(JToken sessionId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "CreateTask");
        }

        [RemoteMethod(name = "StartTask", description = "Signals the start of a given task")]
        public void StartTask(JToken sessionId, JToken taskId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "StartTask", taskId.ToObject<int>());
        }

        [RemoteMethod(name = "EndTask", description = "Signals the end of a given task")]
        public void EndTask(JToken sessionId, JToken taskId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "EndTask", taskId.ToObject<int>());
        }

        [RemoteMethod(name = "CreateResource", description = "Notifies the creation of a new resource")]
        public void CreateResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "CreateResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "DeleteResource", description = "Signals the deletion of a given resource")]
        public void DeleteResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "DeleteResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "BlockedOnResource", description = "")]
        public void BlockedOnResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "BlockedOnResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "BlockedOnAnyResource", description = "")]
        public void BlockedOnAnyResource(JToken sessionId, JArray resourceIds)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "BlockedOnAnyResource", resourceIds.Select(id => id.ToObject<int>()).ToArray());
        }

        [RemoteMethod(name = "SignalUpdatedResource", description = "")]
        public void SignalUpdatedResource(JToken sessionId, JToken resourceId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "SignalUpdatedResource", resourceId.ToObject<int>());
        }

        [RemoteMethod(name = "CreateNondetBool", description = "")]
        public bool CreateNondetBool(JToken sessionId)
        {
            return (bool)RouteRemoteCall(sessionId.ToObject<string>(), "CreateNondetBool");
        }

        [RemoteMethod(name = "CreateNondetInteger", description = "")]
        public int CreateNondetInteger(JToken sessionId, JToken maxValue)
        {
            return (int)RouteRemoteCall(sessionId.ToObject<string>(), "CreateNondetInteger", maxValue.ToObject<int>());
        }

        [RemoteMethod(name = "Assert", description = "")]
        public void Assert(JToken sessionId, JToken value, JToken message)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "Assert", value.ToObject<bool>(), message.ToObject<string>());
        }

        [RemoteMethod(name = "ContextSwitch", description = "Signals the deletion of a given resource")]
        public void ContextSwitch(JToken sessionId)
        {
            RouteRemoteCall(sessionId.ToObject<string>(), "ContextSwitch");
        }

        [RemoteMethod(name = "WaitForMainTask", description = "Wait for test to finish")]
        public string WaitForMainTask(JToken sessionId)
        {
            return (string)RouteRemoteCall(sessionId.ToObject<string>(), "WaitForMainTask");
        }
    }
}