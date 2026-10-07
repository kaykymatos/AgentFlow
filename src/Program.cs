using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

Env.NoClobber().TraversePath().Load();

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

var projectEndpointValue = configuration["FOUNDRY_PROJECT_ENDPOINT"];
if (string.IsNullOrWhiteSpace(projectEndpointValue))
    throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT is not set in environment variables or user secrets.");

var projectEndpoint = new Uri(projectEndpointValue);

var deployment = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"];
if (string.IsNullOrWhiteSpace(deployment))
    throw new InvalidOperationException("AZURE_AI_MODEL_DEPLOYMENT_NAME is not set in environment variables or user secrets.");

var client = new AIProjectClient(
    projectEndpoint,
    new AzureCliCredential());

AIAgent portugueseAgent = await GetTranslationAgentAsync("Portuguese", client, deployment);
AIAgent spanishAgent = await GetTranslationAgentAsync("Spanish", client, deployment);
AIAgent englishAgent = await GetTranslationAgentAsync("English", client, deployment);

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

string finalOutput = string.Empty;
string agenteAtual = string.Empty;
await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        var executorInfo = CleanExecutor(outputEvent.ExecutorId ?? string.Empty);

        if (agenteAtual != executorInfo.Value)
        {
            agenteAtual = executorInfo.Value;
            Console.WriteLine($"\n[{executorInfo.Key} - {executorInfo.Value}]:");
        }
        finalOutput = outputEvent.Data?.ToString() ?? string.Empty;
        Console.Write(finalOutput);
    }
}

KeyValuePair<string, string> CleanExecutor(string rawId)
{
    int underscoreIndex = rawId.IndexOf('_');

    if (underscoreIndex >= 0 && underscoreIndex < rawId.Length - 1)
    {
        string prefix = rawId.Substring(0, underscoreIndex);
        string cleanId = rawId.Substring(underscoreIndex + 1);
        return new KeyValuePair<string, string>(prefix, cleanId);
    }

    // Caso não haja underscore, retorna a string original em ambos os campos
    return new KeyValuePair<string, string>(rawId, rawId);
}
static async Task<FoundryAgent> GetTranslationAgentAsync(string targetLanguage, AIProjectClient client, string model)
{
    string agentName = $"{targetLanguage}Translator";
    var version = await client.AgentAdministrationClient.CreateAgentVersionAsync(
        agentName,
        new ProjectsAgentVersionCreationOptions(
            new DeclarativeAgentDefinition(model)
            {
                Instructions = $"You are a translation assistant that translates the provided text to {targetLanguage}."
            }));

    return client.AsAIAgent(version);
}