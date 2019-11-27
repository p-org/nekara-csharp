using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nekara.Client
{
    public class TestDefinition
    {
        [Flags]
        public enum MethodKind : short
        {
            None = 0,
            IsAsync = 1,
            ReturnsTaskLike = 2,
            ReturnsNekaraTask = 4
        }

        public MethodInfo Setup;
        public MethodInfo Run;
        public MethodInfo Teardown;
        public MethodKind Kind;

        public TestDefinition(MethodInfo Setup, MethodInfo Run, MethodInfo Teardown)
        {
            this.Setup = Setup;
            this.Run = Run;
            this.Teardown = Teardown;
            this.Kind = 0;

            if (Run.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null)
            {
                Kind = Kind | MethodKind.IsAsync;
            }
            if (Run.ReturnType.GetInterface(typeof(IAsyncResult).Name) != null)
            {
                Kind = Kind | MethodKind.ReturnsTaskLike;
            }
            if (Run.ReturnType == typeof(Nekara.Models.Task))
            {
                Kind = Kind | MethodKind.ReturnsTaskLike | MethodKind.ReturnsNekaraTask;
            }
        }
    }
}