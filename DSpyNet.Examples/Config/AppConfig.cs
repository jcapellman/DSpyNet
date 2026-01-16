// DSpyNet.Examples/Config/AppConfig.cs
namespace DSpyNet.Examples.Config
{
    public class AppConfig
    {
        public LlmConfig LLM { get; set; } = new();
    }

    public class LlmConfig
    {
        public string BaseUrl { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ModelId { get; set; } = "gpt-4o-mini";
    }
}