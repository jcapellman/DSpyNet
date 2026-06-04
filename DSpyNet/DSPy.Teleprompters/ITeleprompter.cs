// DSPy.Teleprompters/ITeleprompter.cs

using System.Threading;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;

namespace DSpyNet.DSPy.Teleprompters
{
    /// <summary>
    /// Interface for all optimizers (Teleprompters).
    /// Takes a student module and a training set, returns an optimized module.
    /// </summary>
    public interface ITeleprompter<TModule> where TModule : Module
    {
        Task<TModule> CompileAsync(TModule student, List<Example> trainset, CancellationToken cancellationToken = default);
    }
}