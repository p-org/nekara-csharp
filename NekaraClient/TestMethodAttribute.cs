using System;

namespace Nekara.Client
{
    /// <summary>
    /// Attribute for declaring the entry point to
    /// a program test.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestMethodAttribute : Attribute
    {
    }
}
