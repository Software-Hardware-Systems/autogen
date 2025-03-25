// Copyright (c) Microsoft Corporation. All rights reserved.
// AzureGenie.cs

//using ApprovalUtilities.Utilities;
using DevTeam.Agents;
using DevTeam.Backend.Agents.Developer;
using DevTeam.Backend.Services;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Backend.Agents;

[TypeSubscription(Consts.TopicName)]
public class AzureGenie(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IPersistentState<SandboxMetadata> state,
    IManageAzure azureService,

    ISemanticTextMemory semanticTextMemory,
    AutoGen.Core.IAgent coreAgent,
    IHostApplicationLifetime hostApplicationLifetime,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<AzureGenie>>? logger = null)
    :
    AiAgent<AzureGenie>(semanticTextMemory, coreAgent, hostApplicationLifetime, id, runtime, logger),
    IHandle<ReadmeCreated>,
    IHandle<CodeCreated>
{
    public async ValueTask HandleAsync(ReadmeCreated item, MessageContext messageContext)
    {
        // TODO: Not sure we need to store the files if we use ACA Sessions
        await state.ReadStateAsync();
        await StoreAsync(state.State.Org, state.State.Repo, TryParseLong(state.State.ParentIssueNumber), TryParseLong(state.State.IssueNumber), "readme", "md", "output", item.Readme);

        // TODO: Read the Topic from the agent metadata
        // TODO: Should we use the topic from the message context?
        // TODO: How to handle multiple topics?
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new ReadmeStored
            {
                Org = state.State.Org,
                Repo = state.State.Repo,
                IssueNumber = state.State.IssueNumber,
                ParentNumber = state.State.ParentIssueNumber,
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(CodeCreated item, MessageContext messageContext)
    {
        // TODO: Not sure we need to store the files if we use ACA Sessions
        await state.ReadStateAsync();
        await StoreAsync(state.State.Org, state.State.Repo, TryParseLong(state.State.ParentIssueNumber), TryParseLong(state.State.IssueNumber), "run", "sh", "output", item.Code);

        await RunInSandbox(state.State.Org, state.State.Repo, TryParseLong(state.State.ParentIssueNumber), TryParseLong(state.State.IssueNumber));

        // TODO: Read the Topic from the agent metadata
        // TODO: Should we use the topic from the message context?
        // TODO: How to handle multiple topics?
        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        var topic = topics?.FirstOrDefault() ?? Consts.TopicName;

        await PublishMessageAsync(
            new SandboxRunCreated
            {
                UserId = state.State.UserId,
                UserMessage = state.State.UserMessage,
            },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async Task StoreAsync(string org, string repo, long parentIssueNumber, long issueNumber, string filename, string extension, string dir, string output)
    {
        await azureService.Store(org, repo, parentIssueNumber, issueNumber, filename, extension, dir, output);
    }

    public async Task RunInSandbox(string org, string repo, long parentIssueNumber, long issueNumber)
    {
        await azureService.RunInSandbox(org, repo, parentIssueNumber, issueNumber);
    }

    private long TryParseLong(object value)
    {
        return long.TryParse(value?.ToString(), out var result) ? result : 0;
    }
}
