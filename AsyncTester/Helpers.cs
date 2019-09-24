using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncTester
{
    public class Helpers
    {
        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static void AsyncLoop(Action action)
        {
            // TODO: Need to handle exceptions - either provide a way to handle it
            //       or throw the error to the main thread.
            //       Any exception thrown here will be swallowed silently!!!
            Task.Run(action).ContinueWith(prev => AsyncLoop(action));   // Will this lead to memory leak?
        }

        public static void AsyncTaskLoop(Func<Task> action)
        {
            // TODO: Need to handle exceptions - either provide a way to handle it
            //       or throw the error to the main thread.
            //       Any exception thrown here will be swallowed silently!!!
            action().ContinueWith(prev => AsyncTaskLoop(action));   // Will this lead to memory leak?
        }

        public static void AsyncTaskLoop(Func<Task> action, CancellationToken token)
        {
            // TODO: Need to handle exceptions - either provide a way to handle it
            //       or throw the error to the main thread.
            //       Any exception thrown here will be swallowed silently!!!
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("... Cancelled");
                return;
            }
            action().ContinueWith(prev => AsyncTaskLoop(action, token));   // Will this lead to memory leak?
        }
    }
}