// DSpyNet.Examples/Program.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DSpyNet.Examples.Config;
using DSpyNet.Examples.Core;
using DSpyNet.Examples.Showcases;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DSpyNet.Examples
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. Загрузка конфигурации
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = configBuilder.Build();
            var appConfig = new AppConfig();
            configuration.Bind(appConfig);

            // 2. Настройка логгера
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConfiguration(configuration.GetSection("Logging"));
                builder.AddConsole();
            });

            // 3. Регистрация примеров
            var examples = new List<ExampleRunner>
            {
                new BasicSentimentExample(appConfig, loggerFactory),
                new CoTStartupExample(appConfig, loggerFactory),
                new OptimizationExample(appConfig, loggerFactory),
                new OptimizationCoproExample(appConfig, loggerFactory),
                new OptimizationMiproExample(appConfig, loggerFactory),
                // Новый пример сохранения
                new PersistenceExample(appConfig, loggerFactory)
            };

            // 4. Меню
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("\n================ DSpyNet Showcase ================");
                Console.WriteLine("Select an example to run:");
                for (int i = 0; i < examples.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {examples[i].Name} - {examples[i].Description}");
                }
                Console.WriteLine("0. Exit");
                Console.Write("\nChoice: ");
                Console.ResetColor();

                var input = Console.ReadLine();
                if (input == "0") break;

                if (int.TryParse(input, out int choice) && choice > 0 && choice <= examples.Count)
                {
                    var runner = examples[choice - 1];
                    try
                    {
                        await runner.RunAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nError running example: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                        Console.ResetColor();
                    }
                    
                    Console.WriteLine("\nPress Enter to continue...");
                    Console.ReadLine();
                }
                else
                {
                    Console.WriteLine("Invalid choice.");
                }
            }
        }
    }
}