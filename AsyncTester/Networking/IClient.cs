using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester.Networking
{
    public interface IClient : IDisposable
    {
        Task<JToken> SendRequest(string func);
        Task<JToken> SendRequest(string func, JArray args);
        Task<JToken> SendRequest(string func, params JToken[] args);
        Task<JToken> SendRequest(string func, params bool[] args);
        Task<JToken> SendRequest(string func, params int[] args);
        Task<JToken> SendRequest(string func, params string[] args);

        void AddRemoteMethod(string name, RemoteMethodAsync handler);
    }
}
