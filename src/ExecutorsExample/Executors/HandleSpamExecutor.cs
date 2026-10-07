using ExecutorsExample.Response;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using System.Text.Json;

namespace ExecutorsExample.Executors
{
    internal sealed partial class HandleSpamExecutor : Executor
    {
        public HandleSpamExecutor() : base("HandleSpamExecutor") { }

        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) => protocolBuilder;

        [MessageHandler]
        private async ValueTask HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
        {
            if (message.IsSpam)
            {
                await context.YieldOutputAsync($"Email marked as spam: {message.Reason}");
            }
            else
            {
                throw new ArgumentException("This executor should only handle spam messages.");
            }
        }
    }
}
