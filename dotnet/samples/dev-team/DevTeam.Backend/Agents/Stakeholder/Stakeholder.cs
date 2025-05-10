// Copyright (c) Microsoft Corporation. All rights reserved.
// Stakeholder.cs

using DevTeam.Agents;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
//using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.Stakeholder;

[TypeSubscription(SkillPersona.Stakeholder)]
public class Stakeholder(
    //[FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    ISemanticTextMemory semanticTextMemory,
    IChatClient chatClient,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<Stakeholder>>? logger = null)
    :
    AiAgent<Stakeholder>(semanticTextMemory, chatClient, id, runtime, logger),
    IHandle<StakeholderAsk>,
    IHandle<StakeholderAnswer>,
    IHandle<StakeholderReview>,
    IHandle<StakeholderApprove>
{
    protected sealed class StakeholderState
    {
        public AiAgentConversationState ConversationState { get; } = new();
    }

    public async ValueTask HandleAsync(StakeholderAsk ask, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse("Ask", ask.UserName, ask.UserMessage);
        await PublishMessageAsync(
            new StakeholderAsked { Response = response },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Stakeholder)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(StakeholderAnswer answer, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse("Answer", answer.UserName, answer.UserMessage);
        await PublishMessageAsync(
            new StakeholderAnswered { Response = response },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Stakeholder)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(StakeholderReview review, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse("Review", review.UserName, review.UserMessage);
        await PublishMessageAsync(
            new StakeholderReviewed { Response = response },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Stakeholder)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(StakeholderApprove approve, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse("Approve", approve.UserName, approve.UserMessage);
        await PublishMessageAsync(
            new StakeholderApproved { Response = response },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Stakeholder)
        ).ConfigureAwait(false);
    }

    private async Task<string> GenerateStakeholderResponse(string activity, string authorName, string authorAsk)
    {
        try
        {
            string taskSpecificInstructions = $"Stakeholder activity: {activity}";
            string knowledgeCollection = "Stakeholder Best Practices";
            await AddKnowledgeInstructions(taskSpecificInstructions, knowledgeCollection);

            return await GenerateResponseUsing($"Stakeholder {activity} Prompt", authorName, authorAsk);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, $"Error handling stakeholder {activity.ToLower()}");
            return "";
        }
    }
}

// Message contracts for StakeholderActivity
public class StakeholderAsk
{
    public string UserName { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
}
public class StakeholderAsked
{
    public string Response { get; set; } = string.Empty;
}

public class StakeholderAnswer
{
    public string UserName { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
}
public class StakeholderAnswered
{
    public string Response { get; set; } = string.Empty;
}

public class StakeholderReview
{
    public string UserName { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
}
public class StakeholderReviewed
{
    public string Response { get; set; } = string.Empty;
}

public class StakeholderApprove
{
    public string UserName { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
}
public class StakeholderApproved
{
    public string Response { get; set; } = string.Empty;
}
