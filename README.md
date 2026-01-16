# DSpyNet 🧠

**DSpyNet** is a C# .NET port of the Stanford [DSPy](https://github.com/stanfordnlp/dspy) framework.

It allows you to **program** language models rather than prompt them. Instead of tweaking string prompts manually, you define **Signatures** (Input/Output contracts) and **Modules**, and let **Optimizers** (Teleprompters) automatically tune the prompts and select the best few-shot examples for your specific metrics.

Built on top of **Microsoft Semantic Kernel**.

[![NuGet](https://img.shields.io/nuget/v/DSpyNet.svg)](https://www.nuget.org/packages/DSpyNet)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## 📦 Installation

Install the library via NuGet:

```bash
dotnet add package DSpyNet
```

---

## 🚀 Features & Comparison

DSpyNet adapts the dynamic nature of Python's DSPy to the strongly-typed world of .NET.

| Feature | Original DSPy (Python) | DSpyNet (C#) | Status |
| :--- | :--- | :--- | :--- |
| **Core Abstraction** | Declarative Pydantic Models | C# Classes with Attributes (`[DspInput]`) | ✅ Implemented |
| **LLM Backend** | `dspy.LM` (LiteLLM) | `Microsoft.SemanticKernel` (wrapped in `ILM`) | ✅ Implemented |
| **Modules** | `Predict`, `ChainOfThought` | `Predict<T>`, `ChainOfThought<T>` | ✅ Implemented |
| **Optimizers (Basic)** | `BootstrapFewShot` | `BootstrapFewShot` (Teacher/Student) | ✅ Implemented |
| **Optimizers (Adv)** | `MIPROv2` (Bayesian/Optuna) | `MIPRO` (Bayesian via SharpLearning) | ✅ Implemented |
| **Metrics** | Python Functions | C# Delegates `Func<Example, Prediction, bool>` | ✅ Implemented |
| **Tracing** | Global Context Manager | `AsyncLocal` Execution State | ✅ Implemented |
| **Serialization** | Pickle / JSON | JSON State Serialization | ✅ Implemented |
| **Agents** | `ReAct` | Not yet implemented | 🚧 Planned |

---

## ⚡ Quick Start

### 1. Define a Signature
Instead of writing a prompt text, define what you need using a C# class and attributes.

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
DSpyNet wraps Semantic Kernel to communicate with LLMs (OpenAI, Azure, HuggingFace, etc.).

```csharp
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Clients;

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion("gpt-4o", "YOUR_API_KEY");
var kernel = builder.Build();

// Convert Kernel to DSpy ILM (Language Model interface)
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
// This runs the pipeline, checks results, and "learns" from successes
var optimizer = new BootstrapFewShot<ChainOfThought<QASignature>>(
    metric: exactMatch, 
    maxBootstrappedDemos: 4
);

// Returns a NEW module with optimized prompts and demos embedded
var compiledProgram = await optimizer.CompileAsync(student, trainset);

// 5. Run Optimized Program
var result = await compiledProgram.InvokeAsync(new { Question = "What is 5 + 5?" });
```

---

## 🎮 Interactive Examples (Showcase)

The repository includes a `DSpyNet.Examples` console application that demonstrates real-world usage scenarios.

### Included Scenarios:
1.  **Basic Sentiment Analysis**: Simple Input -> Output using `Predict`.
2.  **Chain of Thought (Startup VC)**: Complex reasoning using `ChainOfThought` to evaluate startup pitches.
3.  **Optimization (BootstrapFewShot)**: A self-improving Intent Classifier bot that learns from examples.

### How to run examples:
1.  Clone the repository.
2.  Navigate to `DSpyNet.Examples`.
3.  Edit `appsettings.json` to include your API Key (OpenAI, RouterAI, or local LLM).
4.  Run:
    ```bash
    dotnet run
    ```

---

## 🏗 Architecture & Mutability

In Python, DSPy modifies classes on the fly. In C#, classes are static. 
DSpyNet solves this by separating **Schema** (Type) from **State** (Data).

*   **`Signature` (Class):** Defines the structure, types, and default instructions (Immutable).
*   **`SignatureState` (Object):** Holds the *actual* instruction text and the list of Few-Shot examples (Mutable).

Optimizers (like MIPRO or Bootstrap) clone the `Module`, modify the `SignatureState` (changing instructions or adding demos), and return a new instance of the module.

---

## 🤝 Contributing

This is an active port. PRs are welcome!