// Copyright (c) Microsoft Corporation. All rights reserved.
// AzureGenie.cs

using DevTeam.Backend.Agents.Developer;
using DevTeam.Backend.Services;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;

namespace DevTeam.Backend.Agents;

[TypeSubscription(SkillPersona.AzureGenie)]
public class AzureGenie(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IManageAzure azureService,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AzureGenie>? logger = null)
    :
    BaseAgent(id, runtime, nameof(AzureGenie), logger),
    IHandle<ReadmeCreated>,
    IHandle<CodeCreated>
{
    public async ValueTask HandleAsync(ReadmeCreated readmeCreated, MessageContext messageContext)
    {
        var (org, repo, issueNumber, parentIssueNumber) = ExtractDetailsFromTopicSource(messageContext.Topic);

        // TODO: Not sure we need to store the files if we use ACA Sessions
        await StoreAsync(org, repo, parentIssueNumber, issueNumber, "readme", "md", "output", readmeCreated.Readme);

        // Get the topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillPersona.DevTeam;

        await PublishMessageAsync(
            new ReadmeStored { },
            topic: messageContext.Topic ?? new TopicId(topic)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(CodeCreated codeCreated, MessageContext messageContext)
    {
        var (org, repo, issueNumber, parentIssueNumber) = ExtractDetailsFromTopicSource(messageContext.Topic);

        // TODO: Not sure we need to store the files if we use ACA Sessions
        await StoreAsync(org, repo, parentIssueNumber, issueNumber, "run", "sh", "output", codeCreated.Code);

        await RunInSandbox(org, repo, parentIssueNumber, issueNumber);

        // Read the Topic from the agent metadata
        var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
        // TODO: How to handle multiple topics?
        var topic = topics?.FirstOrDefault() ?? SkillPersona.DevTeam;

        await PublishMessageAsync(
            new SandboxRunCreated
            {
                UserName = codeCreated.UserName,
                UserMessage = codeCreated.UserMessage,
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

    private (string Org, string Repo, long IssueNumber, long ParentIssueNumber) ExtractDetailsFromTopicSource(TopicId? topicId)
    {
        if (string.IsNullOrEmpty(topicId?.Source))
        {
            throw new ArgumentNullException(nameof(topicId), "TopicId cannot be null");
        }

        var parts = topicId.Value.Source.Split('-');
        if (parts.Length >= 4)
        {
            return (
                Org: parts[0],
                Repo: parts[1],
                IssueNumber: TryParseLong(parts[2]),
                ParentIssueNumber: TryParseLong(parts[3])
            );
        }
        return (
            Org: parts[0],
            Repo: parts[1],
            IssueNumber: TryParseLong(parts[2]),
            ParentIssueNumber: 0
        );
    }

    private long TryParseLong(object? value)
    {
        return long.TryParse(value?.ToString(), out var result) ? result : 0;
    }
}
