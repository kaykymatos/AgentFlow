using Azure.AI.Projects;
using Azure.Identity;
using CustomCoreAgentLib;
using DotNetEnv;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

Env.NoClobber().TraversePath().Load();

// Build configuration that prefers user secrets, then environment variables
var config = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables()
    .Build();

string projectEndpoint = config["FOUNDRY_PROJECT_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("Configuration value 'FOUNDRY_PROJECT_ENDPOINT' is not set");

string deploymentName = config["AZURE_AI_MODEL_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME")
    ?? "gpt-4.1-mini";

var client = new AIProjectClient(new Uri(projectEndpoint), new AzureCliCredential());

AIAgent portugueseAgent = await AgentFactory.CreateAgent(client, "PortugueseTranslator", deploymentName, "You are a translation assistant that translates the provided text to Portuguese.");
AIAgent spanishAgent = await AgentFactory.CreateAgent(client, "SpanishTranslator", deploymentName, "You are a translation assistant that translates the provided text to Spanish.");
AIAgent englishAgent = await AgentFactory.CreateAgent(client, "EnglishTranslator", deploymentName, "You are a translation assistant that translates the provided text to English.");

var workflow = new WorkflowBuilder(portugueseAgent)
            .AddEdge(portugueseAgent, spanishAgent)
            .AddEdge(spanishAgent, englishAgent)
            .Build();

Console.WriteLine("=================================");
Console.WriteLine(" Portuguese -> Spanish -> English");
Console.WriteLine("=================================");
Console.WriteLine();
Console.Write("Digite uma frase para ver a tradução: ");
string? input = Console.ReadLine();

if (string.IsNullOrWhiteSpace(input))
    return;

await using var run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, input));
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

string agenteAtual = string.Empty;
await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        if (agenteAtual != outputEvent.ExecutorId)
        {
            agenteAtual = outputEvent.ExecutorId;
            Console.WriteLine($"\n[{outputEvent.ExecutorId}]:");
        }
        Console.Write(outputEvent.Data?.ToString() ?? string.Empty);
    }
}