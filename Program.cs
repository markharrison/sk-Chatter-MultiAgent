using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics.Contracts;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;

#pragma warning disable SKEXP0001, SKEXP0003, SKEXP0003, SKEXP0011, SKEXP0020, SKEXP0050, SKEXP0052, SKEXP0055, SKEXP0011, SKEXP0010, SKEXP0070

namespace ChatterMultiAgent
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("*** Chatter Agents ***");

            AppSettings setx = new();

            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.Services.ConfigureHttpClientDefaults(
                c => c.AddStandardResilienceHandler(
                    c2 => {
                        TimeSpan timeSpan = TimeSpan.FromMinutes(2);
                        c2.AttemptTimeout.Timeout = timeSpan;
                        c2.CircuitBreaker.SamplingDuration = timeSpan * 2;
                        c2.TotalRequestTimeout.Timeout = timeSpan * 3;
                    }
                ));
            kernelBuilder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Warning));

            kernelBuilder.AddAzureOpenAIChatCompletion(setx.azopenaiCCDeploymentname, setx.azopwnaiEndpoint, setx.azopwnaiApikey);

            var kernel = kernelBuilder.Build();

            BingConnector bing = new BingConnector(setx.bingApikey);
            kernel.ImportPluginFromObject(new WebSearchEnginePlugin(bing), "bing");

            var agentsEngine = new AgentsEnginePoet(kernel);
            await agentsEngine.RunAgents();

        }

    }
}
