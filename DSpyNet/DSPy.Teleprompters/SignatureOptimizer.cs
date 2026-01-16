// DSpyNet/DSPy.Teleprompters/SignatureOptimizer.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;

namespace DSpyNet.DSPy.Teleprompters
{
    /// <summary>
    /// Legacy wrapper for COPRO. 
    /// Optimizes the instructions and prefixes of a module.
    /// </summary>
    public class SignatureOptimizer<TModule> : COPRO<TModule> where TModule : Module
    {
        public SignatureOptimizer(
            ILM promptModel,
            Metric metric,
            int breadth = 5,
            int depth = 3,
            ILogger logger = null) 
            : base(promptModel, metric, breadth, depth, logger)
        {
            if (logger != null)
            {
                logger.LogWarning("SignatureOptimizer is deprecated. Please use COPRO instead.");
            }
        }
    }
}