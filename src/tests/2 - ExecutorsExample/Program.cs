using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using ExecutorExample.Executors;
using ExecutorExample.Response;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

Env.NoClobber().TraversePath().Load();

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true)
    .Build();

var projectEndpoint = new Uri(configuration["FOUNDRY_PROJECT_ENDPOINT"]);
var deploymentName = configuration["AZURE_AI_MODEL_DEPLOYMENT_NAME"]?? "gpt-4.1-mini";

#pragma warning disable OPENAI001

var client = new AIProjectClient(
    projectEndpoint,
    new AzureCliCredential())
    .GetProjectOpenAIClient()
    .GetProjectResponsesClient()
    .AsIChatClient(deploymentName);

#pragma warning restore OPENAI001

AIAgent spamDetectionAgent = new ChatClientAgent(
    client,
    new ChatClientAgentOptions
    {
        ChatOptions = new()
        {
            Instructions =
                "You are a spam detection assistant that identifies spam emails.",

            ResponseFormat =
                ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(
                        typeof(DetectionResult)))
        }
    });

AIAgent emailAssistantAgent = new ChatClientAgent(
    client,
    new ChatClientAgentOptions
    {
        ChatOptions = new()
        {
            Instructions =
                "You are an email assistant that helps users draft professional responses to emails.",

            ResponseFormat =
                ChatResponseFormat.ForJsonSchema(
                    AIJsonUtilities.CreateJsonSchema(
                        typeof(EmailResponse)))
        }
    });

var spamDetectionExecutor =
    new SpamDetectionExecutor(spamDetectionAgent);

var emailAssistantExecutor =
    new EmailAssistantExecutor(emailAssistantAgent);

var sendEmailExecutor =
    new SendEmailExecutor();

var handleSpamExecutor =
    new HandleSpamExecutor();

var workflow = new WorkflowBuilder(spamDetectionExecutor)
    .AddEdge(
        spamDetectionExecutor,
        emailAssistantExecutor)
    .AddEdge(
        emailAssistantExecutor,
        sendEmailExecutor)
    .AddEdge(
        spamDetectionExecutor,
        handleSpamExecutor)
    .WithOutputFrom(
        handleSpamExecutor,
        sendEmailExecutor)
    .Build();

var emailContent = "Congratulations! You've won $1,000,000! Click here to claim your prize now!";

await using var run =await InProcessExecution.RunStreamingAsync(workflow,new ChatMessage(ChatRole.User, emailContent));
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        Console.WriteLine(
            $"WorkflowOutputEvent: {outputEvent}");
    }
}