// Copyright (c) Microsoft Corporation. All rights reserved.
// DeveloperLead.cs

using AutoGen.Core;
using DevTeam.Agents;
using DevTeam.Backend.Agents.Developer;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.DeveloperLead;

[TypeSubscription(Consts.TopicName)]
public class DeveloperLead(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IPersistentState<DeveloperLeadMetadata> state,
    ISemanticTextMemory semanticTextMemory,
    AutoGen.Core.IAgent coreAgent,
    IHostApplicationLifetime hostApplicationLifetime,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<DeveloperLead>>? logger = null)
    :
    AiAgent<DeveloperLead>(semanticTextMemory, coreAgent, hostApplicationLifetime, id, runtime, logger),
    ILeadDevelopers,
    IHandle<DevPlanRequested>,
    IHandle<DevPlanChainClosed>
{
    public async ValueTask HandleAsync(DevPlanRequested item, MessageContext messageContext)
    {
        var planCandidate = await CreatePlan(item.Ask);
        state.State.PlanCandidates.Add(planCandidate);

        // TODO: Read the Topic from the agent metadata
        // TODO: Should we use the topic from the message context?
        // TODO: How to handle multiple topics?
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new DevPlanGenerated
            {
                Org = item.Org,
                Repo = item.Repo,
                IssueNumber = item.IssueNumber,
                Plan = planCandidate
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(DevPlanChainClosed item, MessageContext messageContext)
    {
        var lastPlan = state.State.PlanCandidates.Last();
        // TODO: Read the Topic from the agent metadata
        // TODO: Should we use the topic from the message context?
        // TODO: How to handle multiple topics?
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new DevPlanCreated
            {
                Plan = lastPlan
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task<string> CreatePlan(string ask)
    {
        try
        {
            var context = state.State.ChatHistory.ToString() ?? "";
            var instruction = "Consider the following architectural guidelines:!waf!";
            await AddKnowledge(instruction, context);
            return await CallFunction(DevLeadSkills.Plan);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error creating development plan");
            return "";
        }
    }
}

public class DeveloperLeadMetadata
{
    public List<string> PlanCandidates { get; set; } = new();
    public List<IMessage> ChatHistory { get; set; } = new();
}

public interface ILeadDevelopers
{
    public Task<string> CreatePlan(string ask);
}
