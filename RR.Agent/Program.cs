using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var url = configuration["AzureAIFoundry:Url"];
var model = configuration["AzureAIFoundry:DefaultModel"];
var persistentAgentsClient = new PersistentAgentsClient(
    url,
    new AzureCliCredential());

// Create a persistent agent
var agentMetadata = await persistentAgentsClient.Administration.CreateAgentAsync(
    model: model,
    name: "Joker",
    instructions: "You are good at telling jokes.");

// Retrieve the agent that was just created as an AIAgent using its ID
AIAgent agent1 = await persistentAgentsClient.GetAIAgentAsync(agentMetadata.Value.Id);

// Invoke the agent and output the text result.

var result = await agent1.RunAsync("Tell me a joke about a chicken.");

await foreach(var message in agent1.RunStreamingAsync("Tell me a joke about a chicken."))
{
    Console.Write(message.Text);
}
Console.ReadLine();