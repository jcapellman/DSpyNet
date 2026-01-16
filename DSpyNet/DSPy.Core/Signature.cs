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
            if (!typeof(IDSpySignature).IsAssignableFrom(type))
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