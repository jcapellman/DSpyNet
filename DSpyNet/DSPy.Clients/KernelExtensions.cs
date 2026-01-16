// DSpyNet/DSPy.Clients/KernelExtensions.cs
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Clients
{
    /// <summary>
    /// Extension methods to easily convert a Semantic Kernel instance into a DSPy ILM.
    /// </summary>
    public static class KernelExtensions
    {
        public static ILM ToDSpyLM(this Kernel kernel, PromptExecutionSettings? settings = null)
        {
            return new SemanticKernelLM(kernel, settings);
        }
    }
}