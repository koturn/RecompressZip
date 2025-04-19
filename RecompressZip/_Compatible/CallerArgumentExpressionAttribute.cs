#if !NETCOREAPP3_0_OR_GREATER


#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace System.Runtime.CompilerServices
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Tags parameter that should be filled with specific caller name.
    /// </summary>
    /// <param name="parameterName">Function parameter to take the name from.</param>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute(string parameterName)
        : Attribute
    {
        /// <summary>
        /// Gets name of the function parameter that name should be taken from.
        /// </summary>
        public string ParameterName { get; } = parameterName;
    }
}


#endif  // !NETCOREAPP3_0_OR_GREATER
