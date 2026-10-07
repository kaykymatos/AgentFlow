using ExecutorsExample.Response;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using System.Text.Json;

namespace ExecutorsExample.Executors
{
    internal sealed partial class EmailAssistantExecutor : Executor
    {
        private readonly AIAgent _emailAssistantAgent;

        public EmailAssistantExecutor(AIAgent emailAssistantAgent) : base("EmailAssistantExecutor")
        {
            this._emailAssistantAgent = emailAssistantAgent;
        }

        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) => protocolBuilder;

        [MessageHandler]
        private async ValueTask<EmailResponse> HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (message.IsSpam)
            {
                throw new ArgumentException("This executor should only handle non-spam messages.");
            }

            // Retrieve the email content from shared state
            var email = await context.ReadStateAsync<Email>(message.EmailId, scopeName: EmailStateConstants.EmailStateScope)
                ?? throw new InvalidOperationException("Email not found.");

            // Invoke the agent to draft a response
            var response = await this._emailAssistantAgent.RunAsync(email.EmailContent);
            var emailResponse = JsonSerializer.Deserialize<EmailResponse>(response.Text);

            return emailResponse!;
        }
    }
}
