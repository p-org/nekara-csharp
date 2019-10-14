using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncTester.Core;

namespace AsyncTester.Client
{
    class TestRuntimeLockFactory : IAsyncLockFactory
    {
        private ITestingService api;

        public TestRuntimeLockFactory(ITestingService testingService)
        {
            this.api = testingService;
        }

        public IAsyncLock CreateLock(int resourceId)
        {
            return new TestRuntimeLock(this.api, resourceId);
        }
    }
}
