// Copyright (c) Microsoft Corporation. All rights reserved.
// ProductManager.cs

using AutoGen.Core;
using DevTeam.Agents;
using DevTeam.Backend.Agents.Developer;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.ProductManager;

[TypeSubscription(Consts.TopicName)]
public class ProductManager(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IPersistentState<ProductManagerMetadata> state,
    ISemanticTextMemory semanticTextMemory,
    AutoGen.Core.IAgent coreAgent,
    IHostApplicationLifetime hostApplicationLifetime,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<ProductManager>>? logger = null)
    :
    AiAgent<ProductManager>(semanticTextMemory, coreAgent, hostApplicationLifetime, id, runtime, logger),
    IHandle<ReadmeChainClosed>,
    IHandle<ReadmeRequested>,
    IManageProducts
{
    public async ValueTask HandleAsync(ReadmeChainClosed item, MessageContext messageContext)
    {
        var lastReadme = state.State.ReadmeHistory.Last();
        // TODO: Read the Topic from the agent metadata
        // TODO: Should we use the topic from the message context?
        // TODO: How to handle multiple topics?
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new ReadmeCreated
            {
                Readme = lastReadme
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(ReadmeRequested item, MessageContext messageContext)
    {
        state.State.Org = item.Org;
        state.State.Repo = item.Repo;
        state.State.IssueNumber = item.IssueNumber;
        var newReadme = await CreateReadme(item.Ask);
        state.State.ReadmeHistory.Add(newReadme);

        // TODO: Read the Topic from the agent metadata
        // TODO: Should we use the topic from the message context?
        // TODO: How to handle multiple topics?
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new ReadmeGenerated
            {
                Org = item.Org,
                Repo = item.Repo,
                IssueNumber = item.IssueNumber,
                Readme = newReadme,
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task<string> CreateReadme(string ask)
    {
        try
        {
            string instruction = "Consider the following architectural guidelines:!waf!";
            await AddKnowledge(instruction, ask);

            // This is what results in the LLM inference in the AiAgent class
            return await CallFunction(PMSkills.Readme);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error creating readme");
            return "";
        }
    }
}

public class ProductManagerMetadata
{
    public List<IMessage> ChatHistory { get; set; } = new();
    public List<string> ReadmeHistory { get; set; } = new();
    public string Org { get; internal set; } = "";
    public string Repo { get; internal set; } = "";
    public long IssueNumber { get; internal set; }
}

public interface IManageProducts
{
    public Task<string> CreateReadme(string ask);
}
