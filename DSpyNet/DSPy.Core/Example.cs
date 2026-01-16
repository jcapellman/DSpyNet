// DSpyNet/DSPy.Core/Example.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DSpyNet.DSPy.Core
{
    /// <summary>
    /// Represents a data point (input or output) in DSPy.
    /// Wraps a Dictionary but provides type-safe access helpers.
    /// </summary>
    public class Example
    {
        // Public property for JSON Serialization
        [JsonInclude]
        public Dictionary<string, object> Store { get; private set; }
        
        [JsonInclude]
        public HashSet<string> InputKeys { get; private set; }

        [JsonConstructor]
        public Example(Dictionary<string, object> store, HashSet<string> inputKeys)
        {
            Store = store ?? new Dictionary<string, object>();
            InputKeys = inputKeys ?? new HashSet<string>();
        }

        public Example(Dictionary<string, object> data = null, IEnumerable<string> inputKeys = null)
        {
            Store = data != null ? new Dictionary<string, object>(data) : new Dictionary<string, object>();
            InputKeys = inputKeys != null ? new HashSet<string>(inputKeys) : new HashSet<string>();
        }

        public object this[string key]
        {
            get => Store.ContainsKey(key) ? Store[key] : null;
            set => Store[key] = value;
        }

        public T Get<T>(string key)
        {
            if (Store.TryGetValue(key, out var value))
            {
                if (value is T tVal) return tVal;
                
                // Handle JSON Element case (System.Text.Json default deserialization)
                if (value is System.Text.Json.JsonElement element)
                {
                    if (typeof(T) == typeof(string)) return (T)(object)element.ToString();
                    if (typeof(T) == typeof(int) && element.ValueKind == System.Text.Json.JsonValueKind.Number) return (T)(object)element.GetInt32();
                    if (typeof(T) == typeof(double) && element.ValueKind == System.Text.Json.JsonValueKind.Number) return (T)(object)element.GetDouble();
                    if (typeof(T) == typeof(bool)) return (T)(object)element.GetBoolean();
                }

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

        public Example With(string key, object value)
        {
            var newStore = new Dictionary<string, object>(Store)
            {
                [key] = value
            };
            return new Example(newStore, InputKeys);
        }

        public Example WithInputs(params string[] keys)
        {
            return new Example(new Dictionary<string, object>(Store), keys);
        }

        public Example Inputs()
        {
            if (InputKeys == null || InputKeys.Count == 0)
            {
                return new Example(new Dictionary<string, object>());
            }

            var inputData = Store.Where(kv => InputKeys.Contains(kv.Key))
                                  .ToDictionary(k => k.Key, v => v.Value);
            return new Example(inputData, InputKeys);
        }

        public Example Labels()
        {
             var labelData = Store.Where(kv => !InputKeys.Contains(kv.Key))
                                  .ToDictionary(k => k.Key, v => v.Value);
            return new Example(labelData, new List<string>());
        }

        public Example Combine(Example other)
        {
            var newStore = new Dictionary<string, object>(Store);
            if (other != null)
            {
                foreach (var kvp in other.Store)
                {
                    newStore[kvp.Key] = kvp.Value;
                }
            }
            return new Example(newStore, InputKeys);
        }

        public Example Copy()
        {
             return new Example(new Dictionary<string, object>(Store), new HashSet<string>(InputKeys));
        }

        public Dictionary<string, object> ToDictionary() => new Dictionary<string, object>(Store);

        public override string ToString()
        {
            var content = string.Join(", ", Store.Select(kv => $"{kv.Key}={kv.Value}"));
            var inputs = InputKeys.Count > 0 ? $" (inputs={string.Join(",", InputKeys)})" : "";
            return $"Example({content}){inputs}";
        }

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
        [JsonConstructor]
        public Prediction(Dictionary<string, object> store, HashSet<string> inputKeys) : base(store, inputKeys) { }

        public Prediction(Dictionary<string, object> data = null) : base(data) { }
    }
}