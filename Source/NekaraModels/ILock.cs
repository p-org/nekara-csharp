using System;

namespace Nekara.Models
{
    public interface ILock
    {
        IDisposable Acquire();

        void Release();
    }
}
