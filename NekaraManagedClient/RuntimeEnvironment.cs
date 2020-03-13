using System;
using System.Collections.Generic;
using System.Text;

namespace NekaraManaged.Client
{
    public static class RuntimeEnvironment
    {
        public static NekaraManagedClient Client { get; set; }

        static RuntimeEnvironment()
        {
            Client = new NekaraManagedClient();
        }

    }
}
