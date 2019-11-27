using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Nekara.Core
{
    public class RemoteMethodInvocation
    {
        // parameters
        public readonly object Instance;
        public readonly MethodInfo Function;
        public readonly object[] Arguments;

        // run-time data
        public dynamic Result;
        public event EventHandler OnBeforeInvoke;
        public event EventHandler OnSuccess;
        public event EventHandler<Exception> OnError;
        public event EventHandler OnAfterInvoke;
        // private CancellationTokenSource Cts;
        // private Thread InvokingThread;
        // private Task<dynamic> task;

        public RemoteMethodInvocation(object instance, MethodInfo func, object[] args)
        {
            Instance = instance;
            Function = func;
            Arguments = args;

            // Cts = new CancellationTokenSource();
            //InvokingThread = null;
            //task = null;
            Result = null;
        }

        public override string ToString()
        {
            return Helpers.MethodInvocationString(this.Function.Name, Arguments);
        }

        public object Invoke()
        {
            OnBeforeInvoke(this, null);

            try
            {
                /*task = Task<dynamic>.Factory.StartNew(() => {
                    InvokingThread = Thread.CurrentThread;
                    return Function.Invoke(Instance, Arguments);
                });
                Result = task.GetAwaiter().GetResult();*/

                /*if (Function.ReturnType == typeof(void)) Result = Function.Invoke(Instance, Arguments);
                else Result = Function.Invoke(Instance, Arguments);*/

                Result = Function.Invoke(Instance, Arguments);

                return Result;
            }
            catch (TargetInvocationException ex)
            {
                // Console.WriteLine(ex);
                Console.WriteLine("\n[RemoteMethodInvocation]\n  {0}\tTargetInvocation/{1}", this.ToString(), ex.InnerException.GetType().Name);
                Exception inner;
                if (ex.InnerException is AssertionFailureException)
                {
                    inner = ex.InnerException;
                }
                else if (ex.InnerException is AggregateException)
                {
                    Console.WriteLine("\t    {0}\tTargetInvocation/Aggregate/{1}", this.ToString(), ex.InnerException.InnerException.GetType().Name);
                    inner = ex.InnerException.InnerException;
                }
                else if (ex.InnerException is TargetInvocationException)
                {
                    Console.WriteLine("\t    {0}\tTargetInvocation/TargetInvocation/{1}", this.ToString(), ex.InnerException.InnerException.GetType().Name);
                    inner = ex.InnerException.InnerException;
                }
                else {
                    Console.WriteLine(ex);
                    inner = ex.InnerException;
                }
                OnError(this, inner);
                throw inner;
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[RemoteMethodInvocation]\n  {0}\tUnexpected {1}", this.ToString(), ex.GetType().Name);
                Console.WriteLine(ex);
                throw ex;
            }
            finally
            {
                OnAfterInvoke(this, null);
            }
        }

        /*public void Drop()
        {
            Console.WriteLine("  !!! Dropping Request: {0}  ({1})", this.ToString(), task.Status);
            if (task.Status == TaskStatus.Running && InvokingThread != null)
            {
                Console.WriteLine("  !!! Aborting Thread: {0}", InvokingThread.ManagedThreadId);
                InvokingThread.Abort();
            }
            // task.Dispose();
            //Cts.Cancel();
        }*/
    }
}
