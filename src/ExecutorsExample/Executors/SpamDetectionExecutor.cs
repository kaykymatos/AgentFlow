using ExecutorsExample.Response;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ExecutorsExample.Executors
{
    internal sealed partial class SpamDetectionExecutor : Executor
    {
        private readonly AIAgent _spamDetectionAgent;

        public SpamDetectionExecutor(AIAgent spamDetectionAgent) : base("SpamDetectionExecutor")
        {
            this._spamDetectionAgent = spamDetectionAgent;
        }

        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) => protocolBuilder;

        [MessageHandler]
        private async ValueTask<DetectionResult> HandleAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("ENTROU NO SPAM EXECUTOR");
            Console.WriteLine($"MENSAGEM: {message.Text}");
            var newEmail = new Email
            {
                EmailId = Guid.NewGuid().ToString("N"),
                EmailContent = message.Text
            };
            await context.QueueStateUpdateAsync(newEmail.EmailId, newEmail, scopeName: EmailStateConstants.EmailStateScope);

            // Invoke the agent for spam detection
            var response = await this._spamDetectionAgent.RunAsync(message);
            var detectionResult = JsonSerializer.Deserialize<DetectionResult>(response.Text);

            detectionResult!.EmailId = newEmail.EmailId;
            return detectionResult;
        }
    }
}
