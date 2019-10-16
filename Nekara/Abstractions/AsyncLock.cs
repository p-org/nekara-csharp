using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nekara
{
    public class AsyncLock
    {
        public class Releaser : IDisposable
        {
            private TaskCompletionSource<bool> tcs;
            private string label;

            public Task<bool> Task
            {
                get { return this.tcs.Task; }
            }

            public Releaser(AsyncLock holder, string caller = "Creator")
            {
                this.tcs = new TaskCompletionSource<bool>();
                this.label = holder.label;
                Interlocked.Exchange(ref holder.releaser, this);
                // Console.WriteLine("  ... Caller {2} on Thread {1} acquired lock {0}!", this.label, Thread.CurrentThread.ManagedThreadId, caller);
            }

            public void Dispose(string caller = "")
            {
                this.tcs.SetResult(true);
                // Console.WriteLine("  ... Caller {2} on Thread {1} released {0}", this.label, Thread.CurrentThread.ManagedThreadId, caller);
            }

            public void Dispose()
            {
                this.Dispose("Unknown");
            }
        }

        private string label;
        private Releaser releaser;
        private readonly object locker;

        public AsyncLock(string label)
        {
            this.label = label;
            this.releaser = null;
            this.locker = new object();
        }

        public Task<Releaser> Acquire(string caller = "")
        {
            lock (this.locker) // we need this lock because there is a race condition for the this.releaser reference
            {
                if (this.releaser != null) return this.releaser.Task.ContinueWith(prev => new Releaser(this, caller));
                else return Task.FromResult(new Releaser(this, caller));
            }
        }

        public static AsyncLock Create(string label)
        {
            return new AsyncLock(label);
        }

        public static AsyncLock Create()
        {
            return new AsyncLock("Anonymous");
        }
    }
}
