using Newtonsoft.Json.Linq;

namespace Nekara.Core
{
    public struct SessionInfo
    {
        public string id;
        public string assemblyName;
        public string assemblyPath;
        public string methodDeclaringClass;
        public string methodName;
        public int schedulingSeed;

        public SessionInfo(string id, string assemblyName, string assemblyPath, string methodDeclaringClass, string methodName, int schedulingSeed)
        {
            this.id = id;
            this.assemblyName = assemblyName;
            this.assemblyPath = assemblyPath;
            this.methodDeclaringClass = methodDeclaringClass;
            this.methodName = methodName;
            this.schedulingSeed = schedulingSeed;
        }

        public static SessionInfo FromJson(JObject data)
        {
            return new SessionInfo(data["id"].ToObject<string>(),
                data["assemblyName"].ToObject<string>(),
                data["assemblyPath"].ToObject<string>(),
                data["methodDeclaringClass"].ToObject<string>(),
                data["methodName"].ToObject<string>(),
                data["schedulingSeed"].ToObject<int>());
        }
    }
}
