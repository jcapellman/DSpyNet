// DSpyNet.Tests/Core/Test_Example.cs
using System.Collections.Generic;
using DSpyNet.DSPy.Core;
using Xunit;

namespace DSpyNet.Tests.Core
{
    public class Test_Example
    {
        [Fact]
        public void Test_Example_Initialization()
        {
            var example = new Example(new Dictionary<string, object> { { "a", 1 }, { "b", 2 } });
            Assert.Equal(1, example.Get<int>("a"));
            Assert.Equal(2, example.Get<int>("b"));
        }

        [Fact]
        public void Test_Example_WithInputs()
        {
            var example = new Example(new Dictionary<string, object> { { "a", 1 }, { "b", 2 } })
                .WithInputs("a");
            
            var inputs = example.Inputs();
            var labels = example.Labels();

            Assert.Equal(1, inputs.Get<int>("a"));
            Assert.Null(inputs.Get<object>("b"));

            Assert.Null(labels.Get<object>("a"));
            Assert.Equal(2, labels.Get<int>("b"));
        }

        [Fact]
        public void Test_Example_Copy_Without()
        {
            // Python's .without is not directly implemented, but we test immutability
            var example = new Example(new Dictionary<string, object> { { "a", 1 } });
            var copy = example.With("b", 2);

            Assert.Equal(1, example.Get<int>("a"));
            Assert.Null(example.Get<object>("b"));

            Assert.Equal(1, copy.Get<int>("a"));
            Assert.Equal(2, copy.Get<int>("b"));
        }
    }
}