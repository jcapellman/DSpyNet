# DSpyNet 🧠

**DSpyNet** is a C# .NET port of the Stanford [DSPy](https://github.com/stanfordnlp/dspy) framework.

It allows you to **program** language models rather than prompt them. Instead of tweaking string prompts manually, you define **Signatures** (Input/Output contracts) and **Modules**, and let **Optimizers** (Teleprompters) automatically tune the prompts and select the best few-shot examples for your specific metrics.

Built on top of **Microsoft Semantic Kernel**.

---

## 🚀 Features & Comparison

DSpyNet adapts the dynamic nature of Python's DSPy to the strongly-typed world of .NET.

| Feature | Original DSPy (Python) | DSpyNet (C#) | Status |
| :--- | :--- | :--- | :--- |
| **Core Abstraction** | Declarative Pydantic Models | C# Classes with Attributes (`[DspInput]`) | ✅ Implemented |
| **LLM Backend** | `dspy.LM` (Custom/LiteLLM) | `Microsoft.SemanticKernel` (ILM Interface) | ✅ Implemented |
| **Basic Modules** | `Predict`, `ChainOfThought` | `Predict<T>`, `ChainOfThought<T>` | ✅ Implemented |
| **Complex Modules** | `ReAct`, `ProgramOfThought` | Not yet implemented | ❌ Planned |
| **Optimizers** | `BootstrapFewShot` | `BootstrapFewShot` (Teacher/Student) | ✅ Implemented |
| **Advanced Optimizers** | `MIPROv2` (Bayesian/Optuna) | `MIPRO` (Random Search Strategy) | ⚠️ Partial |
| **Prompt Engineering** | `COPRO`, `SignatureOptimizer` | Not yet implemented | ❌ Planned |
| **Metrics** | Python Functions | C# Delegates `Func<Example, Prediction, bool>` | ✅ Implemented |
| **Tracing** | Global Context Manager | `AsyncLocal` Execution State | ✅ Implemented |
| **Serialization** | Pickle / JSON | JSON State Serialization | ✅ Implemented |

---

## 📦 Installation

*Currently, this is a source-only library. Include the `DSpyNet` project in your solution.*

Dependencies:
*   .NET 8.0+
*   Microsoft.SemanticKernel
*   Microsoft.Extensions.Logging

---

## ⚡ Quick Start

### 1. Define a Signature
Instead of writing a prompt text, define what you need using a C# class.

```csharp
using DSpyNet.DSPy.Core;

[DspInstruction("Translate the text to the target language.")]
public class TranslationSignature : IDSpySignature
{
    [DspInput(Prefix = "Text to translate:")]
    public string InputText { get; set; }

    [DspInput(Prefix = "Target Language:")]
    public string Language { get; set; }

    [DspOutput(Prefix = "Translation:")]
    public string TranslatedText { get; set; }
}
```

### 2. Configure Semantic Kernel
DSpyNet wraps Semantic Kernel to communicate with LLMs.

```csharp
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion("gpt-4o", "YOUR_API_KEY");
var kernel = builder.Build();

// Convert to DSpy ILM
var lm = kernel.ToDSpyLM();
```

### 3. Run a Module
Create a predictor based on your signature and run it.

```csharp
var predictor = new Predict<TranslationSignature>(lm);

var result = await predictor.InvokeAsync(new 
{ 
    InputText = "Hello world", 
    Language = "Spanish" 
});

var prediction = (Prediction)result;
Console.WriteLine(prediction.Get<string>("TranslatedText")); 
// Output: Hola Mundo
```

---

## 🧠 Optimization (Teleprompters)

The power of DSPy is **compiling** your program to optimize it. The `BootstrapFewShot` optimizer runs a "Teacher" model over your training data, validates the outputs using your metric, and automatically saves the best examples to the prompt (Few-Shot Learning).

```csharp
// 1. Define Training Data
var trainset = new List<Example>
{
    Example.From(("Question", "2+2?"), ("Answer", "4")),
    Example.From(("Question", "Capital of France?"), ("Answer", "Paris"))
};

// 2. Define a Metric (Correctness check)
Metric exactMatch = (gold, pred) => 
    gold.Get<string>("Answer") == pred.Get<string>("Answer");

// 3. Setup Modules
var student = new ChainOfThought<QASignature>(lm);

// 4. Compile (Optimize)
var optimizer = new BootstrapFewShot<ChainOfThought<QASignature>>(
    metric: exactMatch, 
    maxBootstrappedDemos: 4
);

// This returns a NEW module with optimized prompts and demos embedded
var compiledProgram = await optimizer.CompileAsync(student, trainset);

// 5. Run Optimized Program
var result = await compiledProgram.InvokeAsync(new { Question = "What is 5 + 5?" });
```

---

## 🏗 Architecture Details

### Mutability in a Static Language
In Python, DSPy modifies classes on the fly. In C#, classes are static. 
DSpyNet solves this by separating **Schema** (Type) from **State** (Data).

*   **`Signature` (Class):** Defines the structure, types, and default instructions (Immutable).
*   **`SignatureState` (Object):** Holds the *actual* instruction text and the list of Few-Shot examples (Mutable).

Optimizers (like MIPRO or Bootstrap) clone the `Module`, modify the `SignatureState` (changing instructions or adding demos), and return a new instance of the module.

### Serialization
You can save optimized modules to disk and load them in production:

```csharp
// Save optimized state
await compiledProgram.SaveAsync("optimized_math_bot.json");

// Load later
var productionBot = new ChainOfThought<MathSignature>(lm);
await productionBot.LoadAsync("optimized_math_bot.json");
```

---

## 🧪 Integration Tests

The repository includes a `RealExampleIntegrationTests` project. It contains examples of:
*   **News Generation:** Generating social media posts from raw text.
*   **Content Guard:** Analyzing sentiment and safety using Chain of Thought.
*   **Intent Classification:** Optimizing a classification task using `BootstrapFewShot`.

To run them, you need to set up your API keys (e.g., OpenAI or RouterAI) in the test base class.

---

## 🤝 Contributing

This is an active port. Missing features (ReAct, Code Execution, Advanced Bayesian Optimization) are planned. PRs are welcome!
