// Copyright (c) Microsoft Corporation. All rights reserved.
// DeveloperLead.cs

using DevTeam.Agents;
using DevTeam.Backend.Agents.Developer;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.DeveloperLead;

[TypeSubscription(SkillType.DeveloperLead)]
public class DeveloperLead(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    ISemanticTextMemory semanticTextMemory,
    IChatClient chatClient,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<DeveloperLead>>? logger = null)
    :
    AiAgent<DeveloperLead>(semanticTextMemory, chatClient, id, runtime, logger),
    ILeadDevelopers,
    IHandle<DevPlanRequested>,
    IHandle<DevPlanIssueClosed>
{
    public async ValueTask HandleAsync(DevPlanRequested devPlanRequested, MessageContext messageContext)
    {
        var planCandidate = await CreatePlan(devPlanRequested.UserName, devPlanRequested.UserMessage);

        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillType.DevTeam;

        await PublishMessageAsync(
            new DevPlanGenerated
            {
                DevPlan = planCandidate
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(DevPlanIssueClosed devPlanIssueClosed, MessageContext messageContext)
    {
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillType.DevTeam;

        await PublishMessageAsync(
            new DevPlanCreated
            {
                DevPlan = devPlanIssueClosed.DevPlan
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task<string> CreatePlan(string authorName, string authorAsk)
    {
        try
        {
            string taskSpecificInstructions = "Consider the following guidelines";
            string knowledgeCollection = "Microsoft Azure Well-Architected Framework";
            await AddKnowledgeInstructions(taskSpecificInstructions, knowledgeCollection);

            return await GenerateResponseUsing(DeveloperLeadSkills.Plan, authorName, authorAsk);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error creating development plan");
            return "";
        }
    }
}

public interface ILeadDevelopers
{
    public Task<string> CreatePlan(string authorName, string authorAsk);
}
