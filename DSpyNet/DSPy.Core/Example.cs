// DSpyNet/DSPy.Core/Example.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Represents a data point (input or output) in DSPy.
    /// Wraps a Dictionary but provides type-safe access helpers.
    /// </summary>
    public class Example
    {
        private readonly Dictionary<string, object> _store;
        private readonly HashSet<string> _inputKeys;

        public Example(Dictionary<string, object> data = null, IEnumerable<string> inputKeys = null)
        {
            _store = data != null ? new Dictionary<string, object>(data) : new Dictionary<string, object>();
            _inputKeys = inputKeys != null ? new HashSet<string>(inputKeys) : new HashSet<string>();
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
                    return default;
                }
            }
            return default;
        }

        /// <summary>
        /// Creates a new Example with the specified key-value pair added/updated.
        /// </summary>
        public Example With(string key, object value)
        {
            var newStore = new Dictionary<string, object>(_store)
            {
                [key] = value
            };
            // Preserve input keys
            return new Example(newStore, _inputKeys);
        }

        /// <summary>
        /// Defines which keys are considered "inputs". Returns a NEW Example.
        /// Analogous to dspy.Example.with_inputs()
        /// </summary>
        public Example WithInputs(params string[] keys)
        {
            return new Example(new Dictionary<string, object>(_store), keys);
        }

        /// <summary>
        /// Returns a new Example containing only the input fields.
        /// </summary>
        public Example Inputs()
        {
            if (_inputKeys == null || _inputKeys.Count == 0)
            {
                // If no inputs defined, return empty or full? 
                // Python DSPy raises error if inputs not set when calling inputs(), 
                // but usually returns what matches input_keys.
                // For safety let's return keys that match _inputKeys
                return new Example(new Dictionary<string, object>());
            }

            var inputData = _store.Where(kv => _inputKeys.Contains(kv.Key))
                                  .ToDictionary(k => k.Key, v => v.Value);
            return new Example(inputData, _inputKeys);
        }

        /// <summary>
        /// Returns a new Example containing only the label fields (fields NOT in input_keys).
        /// </summary>
        public Example Labels()
        {
             var labelData = _store.Where(kv => !_inputKeys.Contains(kv.Key))
                                  .ToDictionary(k => k.Key, v => v.Value);
            return new Example(labelData, new List<string>()); // Labels don't have inputs
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
            return new Example(newStore, _inputKeys);
        }

        public Example Copy()
        {
             return new Example(new Dictionary<string, object>(_store), new HashSet<string>(_inputKeys));
        }

        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(_store);

        public override string ToString()
        {
            var content = string.Join(", ", _store.Select(kv => $"{kv.Key}={kv.Value}"));
            var inputs = _inputKeys.Count > 0 ? $" (inputs={string.Join(",", _inputKeys)})" : "";
            return $"Example({content}){inputs}";
        }

        // Helper for tests to initialize easily
        public static Example From(params (string key, object value)[] pairs)
        {
            var dict = new Dictionary<string, object>();
            foreach(var (k,v) in pairs) dict[k] = v;
            return new Example(dict);
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