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
}