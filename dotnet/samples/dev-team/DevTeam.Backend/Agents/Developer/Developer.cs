// Copyright (c) Microsoft Corporation. All rights reserved.
// Developer.cs

using DevTeam.Agents;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.Developer;

[TypeSubscription(SkillPersona.Developer)]
public class Dev(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    ISemanticTextMemory semanticTextMemory,
    IChatClient chatClient,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<Dev>>? logger = null)
    :
    AiAgent<Dev>(semanticTextMemory, chatClient, id, runtime, logger),
    IDevelopApps,
    IHandle<CodeGenerationRequested>,
    IHandle<CodeIssueClosed>
{
    public async ValueTask HandleAsync(CodeGenerationRequested codeGenerationRequested, MessageContext messageContext)
    {
        var code = await GenerateCode(codeGenerationRequested.UserName, codeGenerationRequested.UserMessage);

        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillPersona.DevTeam;

        await PublishMessageAsync(
            new CodeGenerated
            {
                Code = code
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(CodeIssueClosed codeIssueClosed, MessageContext messageContext)
    {
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillPersona.DevTeam;

        await PublishMessageAsync(
            new CodeCreated
            {
                Code = codeIssueClosed.Code
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task<string> GenerateCode(string authorName, string authorAsk)
    {
        try
        {
            string taskSpecificInstructions = "Consider the following guidelines";
            string knowledgeCollection = "Microsoft Azure Well-Architected Framework";
            await AddKnowledgeInstructions(taskSpecificInstructions, knowledgeCollection);

            return await GenerateResponseUsing(DeveloperSkills.Implement, authorName, authorAsk);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error generating code");
            return "";
        }
    }

}

public interface IDevelopApps
{
    public Task<string> GenerateCode(string authorName, string ask);
}
