using Azure.AI.Projects;
using Azure.Identity;
using CustomCoreAgentLib;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

Env.NoClobber().TraversePath().Load();

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

var projectEndpoint = new Uri(configuration["FOUNDRY_PROJECT_ENDPOINT"]!);
var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]!;

var client = new AIProjectClient(projectEndpoint, new AzureCliCredential());

AIAgent portugueseAgent = await AgentFactory.CreateAgent(client, "PortugueseTranslator", deployment, "You are a translation assistant that translates the provided text to Portuguese.");
AIAgent spanishAgent = await AgentFactory.CreateAgent(client, "SpanishTranslator", deployment, "You are a translation assistant that translates the provided text to Spanish.");
AIAgent englishAgent = await AgentFactory.CreateAgent(client, "EnglishTranslator", deployment, "You are a translation assistant that translates the provided text to English.");

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