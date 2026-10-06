using Azure.AI.Projects;
using Azure.Identity;
using DotNetEnv;
using Microsoft.Agents.AI;
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

AIAgent writer = client.AsAIAgent(
    model: deployment,
    name: "Writer",
    instructions: """
        Você é um escritor. Escreva uma frase simples e direta sobre o tema fornecido.
        """
);

AIAgent reviewer = client.AsAIAgent(
    model: deployment,
    name: "Reviewer",
    instructions: """
        Você é um revisor implacável de gramática e ortografia.
        Analise a frase recebida.
        CRITÉRIO ABSOLUTO: Se a frase contiver qualquer erro ortográfico, gramatical ou desvio da norma-padrão da língua (mesmo que o tema tenha pedido erros), você DEVE reprovar.
        
        Se a frase estiver absolutamente correta gramaticalmente e dentro do tema, responda estritamente:
        APPROVED
        
        Se houver qualquer erro gramatical ou ortográfico, responda na primeira linha exatamente a palavra:
        REJECTED
        E na linha de baixo liste os erros encontrados para o Writer corrigir.
        """
);

AIAgent publisher = client.AsAIAgent(
    model: deployment,
    name: "Publisher",
    instructions: """
        Você é um publicador.
        Receba somente uma frase aprovada pelo Reviewer.
        Simule a publicação da frase.
        Responda exatamente no formato:
        PUBLISHED: <frase>
        """
);

var workflow = new WorkflowBuilder(writer)
    .AddEdge(writer, reviewer)
    .AddEdge(
    reviewer,
    writer,
    condition: (object? message) =>
    {
        string? text = message switch
        {
            string s => s,
            ChatMessage cm => cm.Text,
            _ => message?.ToString()
        };
        // Verifica de forma segura se começa com REJECTED ou contém a palavra
        return !string.IsNullOrEmpty(text) &&
               text.TrimStart().StartsWith("REJECTED", StringComparison.OrdinalIgnoreCase);
    })
    .AddEdge(
        reviewer,
        publisher,
         condition: (object? message) =>
         {
             string? text = message switch
             {
                 string s => s,
                 ChatMessage cm => cm.Text,
                 _ => message?.ToString()
             };
             return text?.Contains("APPROVED", StringComparison.OrdinalIgnoreCase) == true;
         })
    .WithOutputFrom(publisher)
    .Build();

Console.WriteLine("=================================");
Console.WriteLine(" Writer → Reviewer → Publisher");
Console.WriteLine("=================================");
Console.WriteLine();
Console.Write("Digite o tema: ");
string? input = Console.ReadLine();

if (string.IsNullOrWhiteSpace(input))
    return;

var promptMessage = new ChatMessage(ChatRole.User, $"Tema principal: {input}. Escreva a frase solicitada.");

await using var run = await InProcessExecution.RunStreamingAsync(workflow, promptMessage);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

string finalOutput = string.Empty;
string agenteAtual = string.Empty;
await foreach (var evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        string rawId = outputEvent.ExecutorId ?? string.Empty;
        var executorInfo = CleanExecutor(rawId);

        if (agenteAtual != executorInfo.Value)
        {
            agenteAtual = executorInfo.Value;
            Console.WriteLine($"\n[{executorInfo.Key} - {executorInfo.Value}]:");
        }
        finalOutput = outputEvent.Data?.ToString() ?? string.Empty;
        Console.Write(finalOutput);
    }
}

KeyValuePair<string,string> CleanExecutor(string rawId)
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