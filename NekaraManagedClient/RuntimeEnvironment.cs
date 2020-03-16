using System;
using System.Collections.Generic;
using System.Text;
// using System;
// using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Nekara.Networking;
using System.Collections.Concurrent;

namespace NekaraManaged.Client
{
    public static class RuntimeEnvironment
    {
        public static NekaraManagedClient Client { get; set; }
        public static bool remoteClient = false;

        static RuntimeEnvironment()
        {
            Client = new NekaraManagedClient();
        }

    }
}