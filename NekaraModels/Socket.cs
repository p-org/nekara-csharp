using System;
// using Nekara.Core;
// using Nekara.Client;
using NekaraManaged.Client;

namespace Nekara.Models
{
    public class Socket
    {
        private static ITestingService Api = RuntimeEnvironment.Client.Api;

        private int id;
        private object data;

        public Socket(int socketId)
        {
            this.id = socketId;
            this.data = null;

            Api.CreateResource(socketId);
        }

        public void Write(object data)
        {
            Console.WriteLine("  Socket/Write {0} yielding control", this.id);
            Api.ContextSwitch();
            Console.WriteLine("  Socket/Write {0} received control", this.id);
            while (true)
            {
                if (this.data == null)
                {
                    this.data = data;
                    Console.WriteLine("  Socket/Write {0} data available now", this.id);
                    Api.SignalUpdatedResource(this.id);
                    break;
                }
                else
                {
                    Console.WriteLine("  Socket/Write {0} blocked due to non-empty buffer", this.id);
                    Api.BlockedOnResource(this.id);
                    continue;
                }
            }
        }

        public object Read()
        {
            Console.WriteLine("  Socket/Read {0} yielding control", this.id);
            Api.ContextSwitch();
            Console.WriteLine("  Socket/Read {0} received control", this.id);
            while (true)
            {
                if (this.data != null)
                {
                    object result = this.data;
                    this.data = null;
                    Console.WriteLine("  Socket/Read {0} buffer emptied now", this.id);
                    Api.SignalUpdatedResource(this.id);
                    return result;
                }
                else
                {
                    Console.WriteLine("  Socket/Read {0} waiting for data", this.id);
                    Api.BlockedOnResource(this.id);
                    continue;
                }
            }
        }
    }
}