using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Nekara.Networking;
using Nekara.Core;
using System.Diagnostics;

namespace Nekara.Client
{
    /// <summary>
    /// The design of this object is quite unconventional, because of the way we want to expose the API to the end-user.
    /// This object is a global singleton, attached to the <see cref="RuntimeEnvironment"/> static class.
    /// </summary>
    public class TestRuntimeApi : ITestingService
    {
        public static Process currentProcess = Process.GetCurrentProcess();
        public static Helpers.MicroProfiler Profiler = new Helpers.MicroProfiler();

        public IClient socket;
        public Dictionary<(string, int), ClientSession> sessions;

        public TestRuntimeApi(IClient socket)
        {
            this.socket = socket;
            this.sessions = new Dictionary<(string, int), ClientSession>();
        }

        public ClientSession CurrentSession {
            get
            {
                return this.sessions.ContainsKey(RuntimeEnvironment.SessionKey.Value) ? this.sessions[RuntimeEnvironment.SessionKey.Value] : null;
            }
        }

        public void InitializeNewSession(string sessionId)
        {
            RuntimeEnvironment.SetCurrentSession(sessionId);
            /*RuntimeEnvironment.SessionId.Value = sessionId;
            if (!RuntimeEnvironment.SessionCounter.ContainsKey(sessionId)) RuntimeEnvironment.SessionCounter.TryAdd(sessionId, 0);
            RuntimeEnvironment.RunNumber.Value = RuntimeEnvironment.SessionCounter[sessionId]++;*/

            lock (sessions)
            {
                this.sessions.Add(RuntimeEnvironment.SessionKey.Value, new ClientSession(ref this.socket, RuntimeEnvironment.SessionKey.Value));

                /*if (CurrentSession == null) this.sessions.Add(RuntimeEnvironment.SessionId.Value, new ClientSession(ref this.socket, RuntimeEnvironment.SessionId.Value));
                else CurrentSession.Reset();*/
                /*if (this.sessions.ContainsKey(RuntimeEnvironment.SessionId.Value)) this.sessions[RuntimeEnvironment.SessionId.Value].Reset();
                else this.sessions.Add(RuntimeEnvironment.SessionId.Value, new ClientSession(ref this.socket, RuntimeEnvironment.SessionId.Value));*/
            }
        }

        /// <summary>
        /// This method is called internally at the end of a test run to clean up any objects
        /// created during the run and to wait for any pending requests to drop.
        /// It is called to ensure there are no objects lying around and interfering
        /// with the next test run.
        /// </summary>
        private void Finish()
        {
            this.sessions[RuntimeEnvironment.SessionKey.Value].Finish();
        }

        /// <summary>
        /// Every <see cref="ITestingService"/> method call made on the client-side (e.g., CreateTask)
        /// calls this method to send a network request to <see cref="NekaraServer"/>.
        /// This method's job is to transparently handle the logistics of the remote method invocation
        /// by providing a synchronous interface to the client-side.
        /// This method handles any exceptions thrown during the network request. In the normal case
        /// (with stable network connection) any exception thrown during the remote method invocation
        /// is a result of the testing service finding a bug, and hence we catch it at this point
        /// and rethrow it to <see cref="NekaraClient"/> to terminate the test run.
        /// </summary>
        private JToken InvokeRemoteMethod(string func, params JToken[] args)
        {
            if (RuntimeEnvironment.SessionKey.Value.Equals(default)) throw new SessionNotFoundException($"Session {RuntimeEnvironment.SessionKey.Value.ToString()} Not Found");
            return sessions[RuntimeEnvironment.SessionKey.Value].InvokeAndHandleException(func, args);
        }

        /* API methods
         * The methods below simply invoke the API methods at the server end remotely via network.
         * */
        public void CreateTask()
        {
            InvokeRemoteMethod("CreateTask");
        }

        public void StartTask(int taskId)
        {
            InvokeRemoteMethod("StartTask", taskId);
        }

        public void EndTask(int taskId)
        {
            InvokeRemoteMethod("EndTask", taskId);
        }

        public void CreateResource(int resourceId)
        {
            InvokeRemoteMethod("CreateResource", resourceId);
        }

        public void DeleteResource(int resourceId)
        {
            InvokeRemoteMethod("DeleteResource", resourceId);
        }

        public void BlockedOnResource(int resourceId)
        {
            InvokeRemoteMethod("BlockedOnResource", resourceId);
        }

        public void BlockedOnAnyResource(params int[] resourceIds)
        {
            InvokeRemoteMethod("BlockedOnAnyResource", new JToken[] { new JArray(resourceIds.Select(id => JToken.FromObject(id))) });
        }

        public void SignalUpdatedResource(int resourceId)
        {
            InvokeRemoteMethod("SignalUpdatedResource", resourceId);
        }

        public bool CreateNondetBool()
        {
            return InvokeRemoteMethod("CreateNondetBool").ToObject<bool>();
        }

        public int CreateNondetInteger(int maxValue)
        {
            return InvokeRemoteMethod("CreateNondetInteger", maxValue).ToObject<int>();
        }

        public void Assert(bool predicate, string s)
        {
            InvokeRemoteMethod("Assert", predicate, s);
        }

        public void ContextSwitch()
        {
            InvokeRemoteMethod("ContextSwitch");
            if (RuntimeEnvironment.PrintVerbosity > 0) Console.Write(".");
        }

        public string WaitForMainTask()
        {
            var serializedResult = InvokeRemoteMethod("WaitForMainTask").ToObject<string>();
            
            this.Finish();

            return serializedResult;
        }
    }
}