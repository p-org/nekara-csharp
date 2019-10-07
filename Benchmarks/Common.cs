using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Benchmarks
{
    public class Specification
    {
        public static void InjectContextSwitch()
        {

        }

        public static void Assert(bool predicate, string message)
        {

        }
    }

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
                // holder.releaser = this;
                Interlocked.Exchange(ref holder.releaser, this);
                Console.WriteLine("  ... Caller {2} on Thread {1} acquired lock {0}!", this.label, Thread.CurrentThread.ManagedThreadId, caller);
            }

            public void Dispose(string caller = "")
            {
                this.tcs.SetResult(true);
                Console.WriteLine("  ... Caller {2} on Thread {1} released {0}", this.label, Thread.CurrentThread.ManagedThreadId, caller);
            }

            public void Dispose()
            {
                this.Dispose("Unknown");
            }
        }

        private string label;
        private Releaser releaser;
        // private CancellationToken token;
        private readonly object locker;

        public AsyncLock(string label)
        {
            this.label = label;
            this.releaser = null;
            this.locker = new object();
        }

        public Task<Releaser> AcquireAsync(string caller = "")
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

    public class MachineLock : IDisposable
    {
        public class Releaser : MachineLock, IDisposable
        {
        }

        public MachineTask<MachineLock.Releaser> AcquireAsync()
        {
            return new MachineTask<MachineLock.Releaser>(() => {
                return new Releaser();
            });
        }

        public static MachineLock Create()
        {
            return new MachineLock();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public TaskAwaiter GetAwaiter()
        {
            return new TaskAwaiter();
        }
    }

    public class MachineTask : Task
    {
        public MachineTask(Action action) : base(action)
        {

        }

        public static MachineTask Run(Action action)
        {
            return new MachineTask(action);
        }

        /*public Task Run(Action action)
        {
            return Task.CompletedTask;
        }*/

        public static MachineTask WhenAll(params MachineTask[] tasks)
        {
            return MachineTask.Run(() => { });
        }
    }

    public class MachineTask<T> : Task<T>
    {
        public MachineTask(Func<T> function) : base(function)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        public static MachineTask<T> Run(Func<T> function)
        {
            return new MachineTask<T>(function);
        }
    }
}
