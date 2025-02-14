using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;


namespace ChatterMultiAgent
{
    public class AppSettings
    {
        public string azopwnaiApikey { get; set; }
        public string azopwnaiEndpoint { get; set; }
        public string azopenaiCCDeploymentname { get; set; }
        public string azopenaiEmbeddingDeploymentname { get; set; }
        public bool traceOn { get; set; }
        public string bingApikey { get; set; }
        public string bingEndpoint { get; set; }

        public ConfigurationManager configuration;

        public AppSettings()
        {
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "development";
            var hostBuilder = Host.CreateApplicationBuilder();
            hostBuilder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
                .AddEnvironmentVariables();

            configuration = hostBuilder.Configuration;

            azopwnaiApikey = configuration["AzOpenAI:ApiKey"] ?? "";
            azopwnaiEndpoint = configuration["AzOpenAI:Endpoint"] ?? "";
            azopenaiCCDeploymentname = configuration["AzOpenAI:ChatCompletionDeploymentName"] ?? "";
            azopenaiEmbeddingDeploymentname = configuration["AzOpenAI:EmbeddingDeploymentName"] ?? "";
            traceOn = bool.TryParse(configuration["TraceOn"], out bool tOn) && tOn;
            bingApikey = configuration["Bing:ApiKey"] ?? "";
            bingEndpoint = configuration["Bing:Endpoint"] ?? "";
        }

    }
}

