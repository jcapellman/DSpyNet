// DSPy.Core/Example.cs

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Represents a data point (input or output) in DSPy.
    /// Wraps a Dictionary but provides type-safe access helpers.
    /// </summary>
    public class Example
    {
        private readonly Dictionary<string, object> _store;

        public Example(Dictionary<string, object> data = null)
        {
            _store = data != null ? new Dictionary<string, object>(data) : new Dictionary<string, object>();
        }

        public object this[string key]
        {
            get => _store.ContainsKey(key) ? _store[key] : null;
            set => _store[key] = value;
        }

        public T Get<T>(string key)
        {
            if (_store.TryGetValue(key, out var value))
            {
                if (value is T tVal) return tVal;
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // Fallback or throw based on preference. Returning default for now.
                    return default;
                }
            }
            return default;
        }

        public Example With(string key, object value)
        {
            var newStore = new Dictionary<string, object>(_store)
            {
                [key] = value
            };
            return new Example(newStore);
        }

        public Example Combine(Example other)
        {
            var newStore = new Dictionary<string, object>(_store);
            if (other != null)
            {
                foreach (var kvp in other._store)
                {
                    newStore[kvp.Key] = kvp.Value;
                }
            }
            return new Example(newStore);
        }

        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(_store);

        public override string ToString()
        {
            return string.Join(", ", _store.Select(kv => $"{kv.Key}={kv.Value}"));
        }
    }

    /// <summary>
    /// A specialized Example representing a prediction result.
    /// </summary>
    public class Prediction : Example
    {
        public Prediction(Dictionary<string, object> data = null) : base(data) { }
    }
}