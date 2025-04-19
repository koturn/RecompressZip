#if !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP3_0_OR_GREATER


#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Diagnostics.CodeAnalysis
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Specifies that a method will never return under any circumstance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : Attribute
    {
    }
}


#endif  // !NETSTANDARD2_1_OR_GREATER && !NETCOREAPP3_0_OR_GREATER
