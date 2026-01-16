// DSpyNet.Tests/Modules/Test_Predict.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using DSpyNet.DSPy.Clients;
using DSpyNet.DSPy.Core;
using DSpyNet.DSPy.Modules;
using Xunit;

namespace DSpyNet.Tests.Modules
{
    // Define a Signature for testing
    [DspInstruction("Translate english to french.")]
    public class TranslationSignature : IDSpySignature
    {
        [DspInput]
        public string English { get; set; }

        [DspOutput]
        public string French { get; set; }
    }

    public class Test_Predict
    {
        [Fact]
        public async Task Test_Predict_Basic_Call()
        {
            // Setup DummyLM to return a pre-formatted response
            // The adapter usually expects "Prefix Value", but let's see how DummyLM behaves with standard adapter
            // Standard adapter will prompt with "French:", so the model should return "Bonjour"
            var lm = new DummyLM(new List<string> { "Bonjour" });
            
            var predictor = new Predict<TranslationSignature>(lm);
            
            var result = await predictor.InvokeAsync(new { English = "Hello" });
            
            Assert.IsType<Prediction>(result);
            var pred = (Prediction)result;
            
            Assert.Equal("Bonjour", pred.Get<string>("French"));
        }

        [Fact]
        public async Task Test_ChainOfThought_Basic_Call()
        {
            // CoT injects "Reasoning".
            // The DummyLM needs to return the reasoning + the answer.
            // CoTAdapter expects "Reasoning: ... \n OutputPrefix: Value"
            
            var response = "I need to translate Hello to French.\nFrench: Bonjour";
            var lm = new DummyLM(new List<string> { response });

            var cot = new ChainOfThought<TranslationSignature>(lm);

            var result = await cot.InvokeAsync(new { English = "Hello" });
            var pred = (Prediction)result;

            Assert.Equal("Bonjour", pred.Get<string>("French"));
            Assert.Equal("I need to translate Hello to French.", pred.Get<string>("Reasoning"));
        }
    }
}