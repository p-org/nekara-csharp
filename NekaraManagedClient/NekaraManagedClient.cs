using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NekaraManaged.Client
{
    public class NekaraManagedClient // : IDisposable
    {
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WithoutSeed(int max_decisions);
        [DllImport("NekaraCore.dll")]
        public static extern void NS_WithSeed(int _seed, int max_decisions);

        private readonly TestRuntimeApi testingApi;
        public Helpers IdGenerator;

        public NekaraManagedClient()
        {
            // TODO: Using 1000 as default value as of Now.
            NS_WithoutSeed(1000);
            this.testingApi = new TestRuntimeApi();
            this.IdGenerator = new Helpers();
        }

        public TestRuntimeApi Api { get { return this.testingApi; } }

    }
}
