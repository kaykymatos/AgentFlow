using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Microsoft.Agents.AI.Foundry;

namespace CustomCoreAgentLib
{
    public class AgentFactory
    {
        public static async Task<FoundryAgent> CreateAgent(AIProjectClient client, string agentName, string model, string instructiions)
        {
            var version = await client.AgentAdministrationClient.CreateAgentVersionAsync(
                agentName,
                new ProjectsAgentVersionCreationOptions(
                    new DeclarativeAgentDefinition(model)
                    {
                        Instructions = instructiions
                    }));

            return client.AsAIAgent(version);
        }
    }
}
