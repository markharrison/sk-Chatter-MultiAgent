using Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.Diagnostics.Contracts;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using OpenAI.Chat;
using ChatterMultiAgent;

Console.WriteLine("*** Chatter Agents ***");

AppSettings setx = new();

var kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.Services.ConfigureHttpClientDefaults(
    c => c.AddStandardResilienceHandler(
        c2 =>
        {
            TimeSpan timeSpan = TimeSpan.FromMinutes(2);
            c2.AttemptTimeout.Timeout = timeSpan;
            c2.CircuitBreaker.SamplingDuration = timeSpan * 2;
            c2.TotalRequestTimeout.Timeout = timeSpan * 3;
        }
    ));
kernelBuilder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Warning));

kernelBuilder.AddAzureOpenAIChatCompletion(setx.azopenaiCCDeploymentname, setx.azopwnaiEndpoint, setx.azopwnaiApikey);

var kernel = kernelBuilder.Build();

//BingConnector bing = new BingConnector(setx.bingApikey);
//kernel.ImportPluginFromObject(new WebSearchEnginePlugin(bing), "bing");

var agentsEngine = new AgentsEnginePoet(kernel);
await agentsEngine.RunAgents();
