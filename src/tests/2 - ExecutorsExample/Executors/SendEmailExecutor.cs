using ExecutorExample.Response;
using Microsoft.Agents.AI.Workflows;

namespace ExecutorExample.Executors
{
    internal sealed partial class SendEmailExecutor : Executor
    {
        public SendEmailExecutor() : base("SendEmailExecutor") { }

        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) => protocolBuilder;

        [MessageHandler]
        private async ValueTask HandleAsync(EmailResponse message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
            await context.YieldOutputAsync($"Email sent: {message.Response}");
    }
}
