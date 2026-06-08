// DSpyNet/DSPy.Core/ILM.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Abstraction for a Language Model to allow swapping between real (Semantic Kernel) and dummy implementations.
    /// </summary>
    public interface ILM
    {
        Task<string> GenerateAsync(string prompt, Dictionary<string, object>? kwargs = null, CancellationToken cancellationToken = default);
    }
}