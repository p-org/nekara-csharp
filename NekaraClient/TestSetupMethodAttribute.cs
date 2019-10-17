using System;
using System.Collections.Generic;
using System.Text;

namespace Nekara.Client
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestSetupMethodAttribute : Attribute
    {
    }
}
