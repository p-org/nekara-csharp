using System.Threading.Tasks;
using Orleans;

namespace Nekara.Orleans
{
    public interface ILockGrain : IGrainWithIntegerKey
    {
        Task Acquire();

        Task Release();
    }
}