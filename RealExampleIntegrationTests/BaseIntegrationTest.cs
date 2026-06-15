// RealExampleIntegrationTests/BaseIntegrationTest.cs
using System;
using Microsoft.SemanticKernel;
using DSpyNet.DSPy.Clients;
using DSpyNet.DSPy.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace RealExampleIntegrationTests
{
    public abstract class BaseIntegrationTest
    {
        protected readonly ITestOutputHelper _output;
        protected readonly ILoggerFactory _loggerFactory;
        protected readonly ILogger _logger;

        protected BaseIntegrationTest(ITestOutputHelper output)
        {
            _output = output;
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddProvider(new XunitLoggerProvider(output));
            });
            _logger = _loggerFactory.CreateLogger(GetType());
        }

        protected ILM GetRouterAiLM()
        {
            // Используем хардкод ключей из примера конфига, так как это интеграционные тесты для демонстрации
            // В реальной жизни лучше использовать UserSecrets или Environment Variables
            var apiKey = "вставить ключ"; 
            var baseUrl = "https://routerai.ru/api/v1";
            var modelId = "openai/gpt-4o-mini-2024-07-18"; // Дешевая и быстрая модель для тестов

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("API Key not found. Please set it in BaseIntegrationTest.");
            }

            var handler = new HttpClientHandler();
            var client = new HttpClient(handler);

            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(
                modelId: modelId,
                apiKey: apiKey,
                endpoint: new Uri(baseUrl),
                httpClient: client
            );
            
            var kernel = builder.Build();
            return kernel.ToDSpyLM();
        }
    }

    // Простой логгер для вывода в Xunit
    public class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XunitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName) => new XunitLogger(_output, categoryName);

        public void Dispose() { }
    }

    public class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XunitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
        }
    }
}