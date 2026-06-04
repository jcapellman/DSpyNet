// DSpyNet/DSPy.Clients/DummyLM.cs
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSpyNet.DSPy.Core;

namespace DSpyNet.DSPy.Clients
{
    /// <summary>
    /// Dummy Language Model for unit testing.
    /// Returns predefined responses in order.
    /// </summary>
    public class DummyLM : ILM
    {
        private readonly List<string> _responses;
        private int _callCount;
        public List<string> History { get; } = new();

        public DummyLM(List<string> responses)
        {
            _responses = responses;
            _callCount = 0;
        }

        public Task<string> GenerateAsync(string prompt, Dictionary<string, object>? kwargs = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            History.Add(prompt);

            if (_callCount < _responses.Count)
            {
                var response = _responses[_callCount];
                _callCount++;
                return Task.FromResult(response);
            }

            return Task.FromResult(string.Empty);
        }
    }
}