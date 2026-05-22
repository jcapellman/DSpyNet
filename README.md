# DSpyNet 🧠

**DSpyNet** is a C# .NET port of the Stanford [DSPy](https://github.com/stanfordnlp/dspy) framework.

It allows you to **program** language models rather than prompt them. Instead of tweaking string prompts manually, you define **Signatures** (Input/Output contracts) and **Modules**, and let **Optimizers** (Teleprompters) automatically tune the prompts and select the best few-shot examples for your specific metrics.

Built on top of **Microsoft Semantic Kernel**.

[![NuGet](https://img.shields.io/nuget/v/DSpyNet.svg)](https://www.nuget.org/packages/DSpyNet)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## 🌟 What is DSpyNet? (For .NET Developers)

If you have never used DSPy, think of **DSpyNet** as **Entity Framework** but for Large Language Models (LLMs). Instead of concatenating raw SQL strings (prompts), you define classes and work with objects.

Here is a breakdown of the core concepts:

### 1. Predict (Basic Prediction)
The most fundamental building block. It turns an LLM call into a typed method call.

*   **The Problem (Classic Way):** You write huge strings: `$"Translate: {text}. Format: JSON..."`. Then you regex-parse the result, catch JSON exceptions, and retry. It's brittle.
*   **The DSpyNet Way:** You define a Class (`Signature`). It's your contract. You say: "I have these inputs and these outputs." The framework handles the prompting and parsing.
*   **Effect:** String Manipulation -> **Strongly Typed Logic**.

### 2. Chain of Thought (Reasoning)
A "magic" logic injection that improves accuracy. Models hallucinate less if they "think" first.

*   **The Problem:** To make a model smart, you have to manually prompt: *"Let's think step by step"*, and then parse the mess of "Thoughts" vs "Final Answer".
*   **The DSpyNet Way:** You just swap `Predict<T>` for `ChainOfThought<T>`. The library automatically rewrites the prompt, forces the model to generate a `Reasoning` field before the answer, and maps it cleanly to your object.
*   **Effect:** Question -> Answer (Hallucinations) **vs** Question -> Logic -> Answer (High Accuracy).

### 3. Bootstrap Few-Shot (Self-Learning)
This is the killer feature. LLMs perform significantly better when shown 3-5 good examples (Few-Shot Prompting).

*   **The Problem:** Where do you get examples? Hand-writing them into strings is tedious and hard to maintain.
*   **The DSpyNet Way:** You provide a raw dataset (Inputs + Expected Outputs) and a metric. The **Optimizer**:
    1.  Runs your data through the model ("Teacher").
    2.  Checks if the model guessed correctly.
    3.  If correct, it **records** the entire trace (Input -> Reasoning -> Output).
    4.  It saves these "golden traces" and automatically injects them into the prompt for future calls.
*   **Effect:** The bot learns from its own successful attempts. You don't write prompts; you write code and data.

### 4. COPRO (Instruction Optimizer)
Automated Prompt Engineering.

*   **The Problem:** You wrote: *"Translate this text"*. It works okay. You change it to *"Translate professionally"*. Slightly better. You waste hours guessing which words the LLM likes.
*   **The DSpyNet Way:** You task COPRO. It asks a powerful LLM (like GPT-4) to propose 10 variations of your instruction. It tests each one against your data/metric and picks the statistically best one.
*   **Effect:** Guesswork -> **Data-Driven Optimization**. The AI writes its own instructions.

### 5. MIPRO (Bayesian Hyperparameter Optimization)
Heavy artillery for production.

*   **The Problem:** You have 10 good examples and 5 different instructions. Which combination works best? Instruction A + Examples 1,2,3? Or Instruction B + Examples 8,9,10? The search space is huge.
*   **The DSpyNet Way:** It uses mathematics (**Bayesian Optimization** via `SharpLearning`). It explores the search space intelligently, learning which combinations yield higher scores.
*   **Effect:** You get a **mathematically optimal prompt** configuration for your specific task.

### 6. GEPA (Reflective Prompt Evolution)
Self-correcting prompts that learn from their own mistakes.

*   **The Problem:** MIPRO can search a fixed space of instruction variants, but it can't actually *understand* why a prompt failed. If your bot misclassifies "elevator stuck, person trapped" as low-urgency, you want the next prompt to specifically address that failure mode.
*   **The DSpyNet Way:** GEPA runs your program, captures the failures, and asks a powerful **reflection LLM** to read the traces + per-predictor feedback and propose a better instruction. Successful variants enter a **Pareto frontier** (best at >=1 training example) so the optimizer explores diverse strategies instead of collapsing to one.
*   **Effect:** Static prompts → **prompts that evolve by analyzing their own failures**, often with much smaller training budgets than MIPRO.

### 🛠 C# Analogy Cheat Sheet

| Concept | C# Analogy | Purpose |
| :--- | :--- | :--- |
| **Signature** | `Interface` / `DTO` | Defines the Data Contract (Inputs/Outputs). |
| **Predict** | `Func<TInput, TOutput>` | Executes the contract via LLM. |
| **ChainOfThought** | `Middleware` pipeline | Injects a "Thinking" step before execution. |
| **BootstrapFewShot** | `Unit Tests` -> `Documentation` | Takes passing tests and turns them into documentation (examples) for the AI. |
| **COPRO** | `A/B Testing` | Automatically rewrites code (instructions) until metrics improve. |
| **MIPRO** | `AutoML` | Tunes hyperparameters to find the perfect system configuration. |
| **GEPA** | `Code Review with Self-Healing` | Reflects on failures, rewrites the prompt, keeps only what improves things. |

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
| **Reflective Opt** | `GEPA` (Reflective Prompt Evolution) | `GEPA<TModule>` (Pareto pool + reflection LM) | ✅ Implemented |
| **Prompt Engineering** | `COPRO`, `SignatureOptimizer` | `COPRO` (Coordinate Ascent) | ✅ Implemented |
| **Metrics** | Python Functions | C# Delegates `Metric` (bool) and `FeedbackMetric` (score + feedback for GEPA) | ✅ Implemented |
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
4.  **Optimization (COPRO)**: Evolving instructions to detect Sarcasm.
5.  **Optimization (MIPRO)**: Bayesian optimization of a RAG Query Rewriter using an LLM-as-a-Judge.
6.  **Optimization (GEPA)**: Reflective prompt evolution on a multi-predictor Facility Support Analyzer (urgency / sentiment / categories).

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

## 🤝 Contributing

This is an active port. PRs are welcome!