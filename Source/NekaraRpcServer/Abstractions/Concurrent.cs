using System.Threading;

namespace Nekara.Abstractions
{
    public class Concurrent<T>
    {
        private object locker = new object();
        private T _Value = default(T);
        
        public T Value
        {
            get
            {
                lock (locker)
                {
                    return _Value;
                }
            }
            set
            {
                lock (locker)
                {
                    _Value = value;
                }
            }
        }
    }
}
