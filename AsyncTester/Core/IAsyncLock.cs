using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester.Core
{
    public interface IAsyncLock
    {
        void Acquire();

        void Release();
    }
}
