// DSpyNet/DSPy.Clients/SemanticKernelLM.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Clients
{
    /// <summary>
    /// Implementation of ILM using Microsoft Semantic Kernel.
    /// </summary>
    public class SemanticKernelLM : ILM
    {
        private readonly Kernel _kernel;
        private readonly PromptExecutionSettings? _defaultSettings;

        public SemanticKernelLM(Kernel kernel, PromptExecutionSettings? defaultSettings = null)
        {
            _kernel = kernel;
            _defaultSettings = defaultSettings;
        }

        public async Task<string> GenerateAsync(string prompt, Dictionary<string, object>? kwargs = null)
        {
            // Here we could merge kwargs with _defaultSettings if needed.
            // For simplicity, we use the prompt directly.
            
            var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments(_defaultSettings));
            return result.GetValue<string>() ?? string.Empty;
        }
    }
}