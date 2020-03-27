using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NekaraManaged.Client
{
    public class NekaraManagedClient // : IDisposable
    {
        [DllImport("NekaraCore.dll")]
        public static extern IntPtr NS_NekaraService();

        private readonly TestRuntimeApi testingApi;
        public Helpers IdGenerator;

        public NekaraManagedClient()
        {
            this.testingApi = new TestRuntimeApi();
            this.testingApi.ns_handle = NS_NekaraService();
            this.IdGenerator = new Helpers();
        }

        public TestRuntimeApi Api { get { return this.testingApi; } }

    }
}
