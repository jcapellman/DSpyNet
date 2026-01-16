// DSpyNet.Examples/Core/ExampleRunner.cs
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Clients; // Расширение ToDSpyLM()
using DSpyNet.DSPy.Core;
using DSpyNet.Examples.Config;

namespace DSpyNet.Examples.Core
{
    /// <summary>
    /// Базовый класс для всех примеров. Настраивает Kernel и ILM.
    /// </summary>
    public abstract class ExampleRunner
    {
        protected readonly ILM _lm;
        protected readonly ILogger _logger;

        protected ExampleRunner(AppConfig config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            // Настройка Semantic Kernel для работы с OpenAI-совместимым API (RouterAI, LocalAI, OpenAI)
            var builder = Kernel.CreateBuilder();
            
            // Используем стандартный HttpClient
            var httpClient = new HttpClient();

            builder.AddOpenAIChatCompletion(
                modelId: config.LLM.ModelId,
                apiKey: config.LLM.ApiKey,
                endpoint: new Uri(config.LLM.BaseUrl),
                httpClient: httpClient
            );

            var kernel = builder.Build();
            
            // Превращаем Kernel в DSpy ILM
            _lm = kernel.ToDSpyLM();
        }

        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Task RunAsync();

        // Хелперы для консоли
        protected void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n=== {title} ===");
            Console.ResetColor();
        }

        protected void PrintInput(string label, string value)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{label}: ");
            Console.ResetColor();
            Console.WriteLine(value);
        }

        protected void PrintOutput(string label, string value)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{label}: ");
            Console.ResetColor();
            Console.WriteLine(value);
        }

        protected void PrintInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }
    }
}