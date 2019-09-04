using Microsoft.PSharp;

namespace Nekara.Tests.PSharp.MultiPaxos
{
    #region Events

    class local : Event { }
    class success : Event { }
    class goPropose : Event { }
    class response : Event { }

    #endregion
}
