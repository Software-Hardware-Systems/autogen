// Copyright (c) Microsoft Corporation. All rights reserved.
// ProductManager.cs

using DevTeam.Agents;
using DevTeam.Backend.Agents.Developer;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.ProductManager;

[TypeSubscription(SkillType.ProductOwner)]
public class ProductManager(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    ISemanticTextMemory semanticTextMemory,
    IChatClient chatClient,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<ProductManager>>? logger = null)
    :
    AiAgent<ProductManager>(semanticTextMemory, chatClient, id, runtime, logger),
    IHandle<ReadmeIssueClosed>,
    IHandle<ReadmeRequested>,
    IManageProducts
{
    protected sealed class ProductManagerState
    {
        public AiAgentConversationState ConversationState { get; } = new();
    }

    public async ValueTask HandleAsync(ReadmeRequested readmeRequested, MessageContext messageContext)
    {
        var newReadme = await GenerateReadme(readmeRequested.UserName, readmeRequested.UserMessage);

        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(ProductManager));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillType.DevTeam;

        await PublishMessageAsync(
            new ReadmeGenerated
            {
                Readme = newReadme,
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(ReadmeIssueClosed readmeIssueClosed, MessageContext messageContext)
    {
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillType.DevTeam;

        await PublishMessageAsync(
            new ReadmeCreated
            {
                Readme = ConversationState.GetLastGeneration(),
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task<string> GenerateReadme(string authorName, string authorAsk)
    {
        try
        {
            string taskSpecificInstructions = "Consider the following guidelines";
            string knowledgeCollection = "Microsoft Azure Well-Architected Framework";
            await AddKnowledgeInstructions(taskSpecificInstructions, knowledgeCollection);

            return await GenerateResponseUsing(PMSkills.Readme, authorName, authorAsk);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error creating readme");
            return "";
        }
    }
}

public interface IManageProducts
{
    public Task<string> GenerateReadme(string authorName, string ask);
}
