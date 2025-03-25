// Copyright (c) Microsoft Corporation. All rights reserved.
// Developer.cs

using AutoGen.Core;
using DevTeam.Agents;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents.Developer;

[TypeSubscription(Consts.TopicName)]
public class Dev(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IPersistentState<DeveloperMetadata> state,

    ISemanticTextMemory semanticTextMemory,
    AutoGen.Core.IAgent coreAgent,
    IHostApplicationLifetime hostApplicationLifetime,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<Dev>>? logger = null)
    :
    AiAgent<Dev>(semanticTextMemory, coreAgent, hostApplicationLifetime, id, runtime, logger),
    IDevelopApps,
    IHandle<CodeGenerationRequested>,
    IHandle<CodeChainClosed>
{
    public async ValueTask HandleAsync(CodeGenerationRequested item, MessageContext messageContext)
    {
        var code = await GenerateCode(item.Ask);
        // TODO: Read the Topic from the agent metadata
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new CodeGenerated
            {
                Org = item.Org,
                Repo = item.Repo,
                IssueNumber = item.IssueNumber,
                Code = code
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(CodeChainClosed item, MessageContext messageContext)
    {
        var lastCode = state.State.CodeCandidates.Last().GetContent();
        // TODO: Read the Topic from the agent metadata
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new CodeCreated
            {
                Code = lastCode
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task<string> GenerateCode(string ask)
    {
        try
        {
            //var context = new KernelArguments { ["input"] = AppendChatHistory(ask) };
            //var instruction = "Consider the following architectural guidelines:!waf!";
            //var enhancedContext = await AddKnowledge(instruction, "waf");
            return await CallFunction(DeveloperSkills.Implement);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error generating code");
            return "";
        }
    }
}

public class DeveloperMetadata
{
    public List<IMessage> CodeCandidates { get; set; } = new();
    public List<IMessage> ChatHistory { get; set; } = new();
}

public interface IDevelopApps
{
    public Task<string> GenerateCode(string ask);
}
