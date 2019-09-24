using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace AsyncTester
{
    public abstract class JsonP2P
    {
        private Dictionary<string, RemoteMethodAsync> remoteMethods;
        private Dictionary<string, TaskCompletionSource<string>> requests;
        public JsonP2P()
        {
            this.remoteMethods = new Dictionary<string, RemoteMethodAsync>();
            this.requests = new Dictionary<string, TaskCompletionSource<string>>();
        }

        public abstract Task Send(string payload);

        public void RegisterRemoteMethod(string name, Func<object, Task<object>> handler)
        {
            RemoteMethodAsync method = new RemoteMethodAsync(handler);
            this.remoteMethods[name] = method;
        }

        public void HandleMessage(string payload)
        {
            Console.WriteLine("Trying to handle message: {0}", payload);
            try
            {
                HandleRequest(payload);
            }
            catch (UnexpectedRequestException e1)
            {
                try
                {
                    HandleResponse(payload);
                }
                catch (UnexpectedResponseException e2)
                {
                    throw new UnexpectedMessageException();
                }
            }
        }

        public void HandleRequest(string payload)
        {
            RequestMessage message = JsonConvert.DeserializeObject<RequestMessage>(payload);
            if (message.func != null && this.remoteMethods.ContainsKey(message.func))
            {
                this.remoteMethods[message.func]((object)message.args);
            }
            else
            {
                throw new UnexpectedRequestException();
            }
        }

        public void HandleResponse(string payload)
        {
            ResponseMessage message = JsonConvert.DeserializeObject<ResponseMessage>(payload);
            if (message.responseTo != null && this.requests.ContainsKey(message.responseTo))
            {
                this.requests[message.responseTo].SetResult(message.data);
            }
            else
            {
                throw new UnexpectedResponseException();
            }
        }

        public Task<string> Request(string func, string args, int timeout = 10000)
        {
            var tcs = new TaskCompletionSource<string>();   // This tcs will be settled when the response comes back
            var message = new RequestMessage(func, args);
            var serialized = JsonConvert.SerializeObject(message);
            this.requests.Add(message.id, tcs);
            this.Send(serialized);

            var timer = new Timer(_ => tcs.SetException(new RequestTimeoutException()), null, timeout, Timeout.Infinite);   // Set a timeout for the request
            tcs.Task.ContinueWith(prev => {
                timer.Change(Timeout.Infinite, Timeout.Infinite);
                this.requests.Remove(message.id);
                timer.Dispose();
            });
            return tcs.Task;
        }

        public Task Respond(string requestId, string data)
        {
            var message = new ResponseMessage(requestId, data);
            var serialized = JsonConvert.SerializeObject(message);
            return this.Send(serialized);
        }
    }

    public delegate Task<object> RemoteMethodAsync(object kwargs);

    public class RemoteMethodAttribute : Attribute
    {
        public string name;
        public string description;
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    public class RequestMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string func;

        [DataMember]
        internal string args;

        public RequestMessage(string func, string args)
        {
            this.id = "req-" + Helpers.RandomString(16);
            this.func = func;
            this.args = args;
        }
    }

    // This Message construct is 1 layer above the communication layer
    // This extra level of abstraction is useful for remaining protocol-agnostic,
    // so that we can plug-in application layer transport protocols
    [DataContract]
    public class ResponseMessage
    {
        [DataMember]
        internal string id;

        [DataMember]
        internal string responseTo;

        [DataMember]
        internal string data;

        public ResponseMessage(string requestId, string data)
        {
            this.id = "res-" + Helpers.RandomString(16);
            this.responseTo = requestId;
            this.data = data;
        }
    }
}
