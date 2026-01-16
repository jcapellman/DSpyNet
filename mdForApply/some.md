Вот исправленные и дополненные файлы. Я изменил уровень доступа свойства `State` в классе `Predict` на `protected set`, чтобы наследники (например, `ChainOfThought`) могли его клонировать, и проверил неймспейсы для `IDSPySignature`.

```csharp
// DSpyNet/DSPy.Core/Attributes.cs
using System;

namespace DSpyNet.DSPy.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class DspInstructionAttribute : Attribute
    {
        public string Instruction { get; }

        public DspInstructionAttribute(string instruction)
        {
            Instruction = instruction;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class DspInputAttribute : Attribute
    {
        public string Description { get; set; }
        public string Prefix { get; set; }

        public DspInputAttribute(string description = "", string prefix = "")
        {
            Description = description;
            Prefix = prefix;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class DspOutputAttribute : Attribute
    {
        public string Description { get; set; }
        public string Prefix { get; set; }

        public DspOutputAttribute(string description = "", string prefix = "")
        {
            Description = description;
            Prefix = prefix;
        }
    }
    
    /// <summary>
    /// Marker interface for all Signatures.
    /// Used to constrain generic types in Modules.
    /// </summary>
    public interface IDSpySignature { }
}
```

```csharp
// DSpyNet/DSPy.Core/Signature.cs
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace DSpyNet.DSPy.Core
{
    public class SignatureField
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Prefix { get; set; }
        public Type Type { get; set; }
        public PropertyInfo PropertyInfo { get; set; }
    }

    /// <summary>
    /// Parses a C# class marked with DSPy attributes to extract metadata.
    /// This represents the schema of a task.
    /// </summary>
    public class Signature
    {
        public string Instruction { get; private set; }
        public List<SignatureField> InputFields { get; private set; } = new();
        public List<SignatureField> OutputFields { get; private set; } = new();
        public Type SignatureType { get; private set; }

        public Signature(Type type)
        {
            if (!typeof(IDSPySignature).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.Name} must implement IDSpySignature");
            }

            SignatureType = type;
            ParseType(type);
        }

        /// <summary>
        /// Clone constructor for deep copying signatures if needed.
        /// </summary>
        public Signature(Signature other)
        {
            Instruction = other.Instruction;
            SignatureType = other.SignatureType;
            // Create new lists but share field definitions (they are metadata)
            InputFields = new List<SignatureField>(other.InputFields);
            OutputFields = new List<SignatureField>(other.OutputFields);
        }

        private void ParseType(Type type)
        {
            // 1. Get Instruction
            var instructionAttr = type.GetCustomAttribute<DspInstructionAttribute>();
            Instruction = instructionAttr?.Instruction ?? "Given the inputs, produce the outputs.";

            // 2. Get Properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var inputAttr = prop.GetCustomAttribute<DspInputAttribute>();
                var outputAttr = prop.GetCustomAttribute<DspOutputAttribute>();

                if (inputAttr != null)
                {
                    InputFields.Add(new SignatureField
                    {
                        Name = prop.Name,
                        Description = inputAttr.Description,
                        Prefix = string.IsNullOrEmpty(inputAttr.Prefix) ? $"{prop.Name}:" : inputAttr.Prefix,
                        Type = prop.PropertyType,
                        PropertyInfo = prop
                    });
                }
                else if (outputAttr != null)
                {
                    OutputFields.Add(new SignatureField
                    {
                        Name = prop.Name,
                        Description = outputAttr.Description,
                        Prefix = string.IsNullOrEmpty(outputAttr.Prefix) ? $"{prop.Name}:" : outputAttr.Prefix,
                        Type = prop.PropertyType,
                        PropertyInfo = prop
                    });
                }
            }
        }
    }
}
```

```csharp
// DSpyNet/DSPy.Modules/Predict.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Collections.Generic;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// The standard predictor module. 
    /// Takes a Signature, builds a prompt via Adapter, calls Semantic Kernel, parses output.
    /// </summary>
    /// <typeparam name="TSignature">The C# class defining the Input/Output schema.</typeparam>
    public class Predict<TSignature> : Module where TSignature : IDSpySignature, new()
    {
        // Changed to protected set to allow subclasses (like ChainOfThought) to set it during cloning
        public SignatureState State { get; protected set; }
        protected readonly Kernel _kernel;
        protected readonly DSPyAdapter _adapter;

        public Predict(Kernel kernel, ILogger logger = null) : base(logger)
        {
            _kernel = kernel;
            _adapter = new DSPyAdapter();
            
            // Initialize State from the static Type definition
            var signature = new Signature(typeof(TSignature));
            State = new SignatureState(signature);
        }

        public override async Task<object> InvokeAsync(object input)
        {
            // Support both Example and anonymous/typed objects
            Example inputExample;
            if (input is Example ex)
            {
                inputExample = ex;
            }
            else
            {
                // Convert object properties to Dictionary
                var dict = new Dictionary<string, object>();
                foreach (var prop in input.GetType().GetProperties())
                {
                    dict[prop.Name] = prop.GetValue(input);
                }
                inputExample = new Example(dict);
            }

            return await ForwardAsync(inputExample);
        }

        public virtual async Task<Prediction> ForwardAsync(Example input)
        {
            // 1. Format Prompt
            var prompt = _adapter.Format(State, input);
            
            _logger?.LogDebug($"[DSPy Predict] Prompt generated:\n{prompt}");

            // 2. Call LLM (Semantic Kernel)
            var result = await _kernel.InvokePromptAsync(prompt);
            var responseText = result.GetValue<string>();

            _logger?.LogDebug($"[DSPy Predict] LLM Response:\n{responseText}");

            // 3. Parse Response
            var prediction = _adapter.Parse(responseText, State);

            // 4. Trace
            if (ExecutionState.IsTracing)
            {
                ExecutionState.AddEntry(new TraceEntry
                {
                    SignatureState = State.Clone(), // Snapshot state at this moment
                    Inputs = input,
                    Outputs = prediction,
                    PromptUsed = prompt
                });
            }

            return prediction;
        }

        public override Module DeepClone()
        {
            var clone = (Predict<TSignature>)this.MemberwiseClone();
            clone.State = this.State.Clone(); // Deep clone mutable state
            return clone;
        }
    }
}
```

```csharp
// DSpyNet/DSPy.Modules/ChainOfThought.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Execution;
using System.Collections.Generic;

namespace DSpyNet.DSPy.Modules
{
    /// <summary>
    /// ChainOfThought module. 
    /// Adds a "Reasoning" output field dynamically before the actual outputs.
    /// </summary>
    public class ChainOfThought<TSignature> : Predict<TSignature> where TSignature : IDSpySignature, new()
    {
        public ChainOfThought(Kernel kernel, ILogger logger = null) : base(kernel, logger)
        {
            // In the future, we can inject the field into State.Signature.OutputFields here
        }

        public override async Task<Prediction> ForwardAsync(Example input)
        {
            // 1. Clone state to ensure thread safety during execution (and allow temp modification if needed)
            var cotState = State.Clone();
            
            // 2. Use the specialized Adapter that knows how to inject/extract Reasoning
            var cotAdapter = new CoTAdapter();
            
            var prompt = cotAdapter.Format(cotState, input);
            _logger?.LogDebug($"[DSPy CoT] Prompt:\n{prompt}");

            // 3. Invoke LLM
            var result = await _kernel.InvokePromptAsync(prompt);
            var responseText = result.GetValue<string>();
            _logger?.LogDebug($"[DSPy CoT] Response:\n{responseText}");

            // 4. Parse using CoT logic
            var prediction = cotAdapter.Parse(responseText, cotState);
            
            // 5. Trace
            if (ExecutionState.IsTracing)
            {
                ExecutionState.AddEntry(new TraceEntry
                {
                    SignatureState = State.Clone(), 
                    Inputs = input,
                    Outputs = prediction,
                    PromptUsed = prompt
                });
            }

            return prediction;
        }
        
        public override Module DeepClone()
        {
            var clone = (ChainOfThought<TSignature>)this.MemberwiseClone();
            // This now works because State set accessor is protected in Predict<T>
            clone.State = this.State.Clone(); 
            return clone;
        }
    }

    /// <summary>
    /// Specialized Adapter for ChainOfThought.
    /// Injects "Reasoning" field logic into the prompt and parser.
    /// </summary>
    public class CoTAdapter : DSPyAdapter
    {
        public override string Format(SignatureState state, Example inputs)
        {
            // Base format gives us the standard template
            var originalFormat = base.Format(state, inputs);
            
            // We want to insert "Reasoning: Let's think step by step" before the first output field prefix.
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            if (firstOutput == null) return originalFormat;

            // Heuristic: Check if the prompt ends with the first output prefix (which standard adapter does)
            if (originalFormat.EndsWith(firstOutput.Prefix))
            {
                var len = firstOutput.Prefix.Length;
                var trimmed = originalFormat.Substring(0, originalFormat.Length - len);
                
                // Add the CoT trigger
                return trimmed + "Reasoning: Let's think step by step.\n" + firstOutput.Prefix;
            }
            
            return originalFormat + "\nReasoning: Let's think step by step.";
        }

        public override Prediction Parse(string llmResponse, SignatureState state)
        {
            var result = new Dictionary<string, object>();
            
            var firstOutput = state.Signature.OutputFields.FirstOrDefault();
            
            if (firstOutput == null) 
            {
                return new Prediction(new Dictionary<string, object> { { "Raw", llmResponse } });
            }

            var firstMarker = firstOutput.Prefix;
            var markerIdx = llmResponse.IndexOf(firstMarker);

            if (markerIdx != -1)
            {
                // Everything before the first real output marker is Reasoning
                var reasoning = llmResponse.Substring(0, markerIdx).Trim();
                result["Reasoning"] = reasoning;
                
                var restOfResponse = llmResponse.Substring(markerIdx);
                // Move past the prefix to get the value of the first field
                var valueStart = firstMarker.Length;
                
                // Safety check
                if (valueStart > restOfResponse.Length) valueStart = restOfResponse.Length;
                
                var valueText = restOfResponse.Substring(valueStart); 
                
                // Use base parser for the structured part
                var basePrediction = base.Parse(valueText, state);
                
                // Merge results
                foreach (var kvp in basePrediction.ToDictionary())
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // Fallback: Assume it's reasoning + implicit answer or failed structure
                result["Reasoning"] = llmResponse;
                
                // Try parsing normally just in case
                var fallback = base.Parse(llmResponse, state);
                foreach(var kvp in fallback.ToDictionary())
                {
                    if (!result.ContainsKey(kvp.Key))
                        result[kvp.Key] = kvp.Value;
                }
            }

            return new Prediction(result);
        }
    }
}
```