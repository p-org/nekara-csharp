using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester.Client
{
    public interface IAsyncLockFactory
    {
        IAsyncLock CreateLock(int resourceId);
    }
}
