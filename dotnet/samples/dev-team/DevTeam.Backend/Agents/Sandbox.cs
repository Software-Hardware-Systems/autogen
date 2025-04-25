// Copyright (c) Microsoft Corporation. All rights reserved.
// Sandbox.cs

using System.Text.Json;
using DevTeam.Agents;
using DevTeam.Backend.Agents.Developer;
using DevTeam.Backend.Services;
using Microsoft.AutoGen.AgentChat.State;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.RuntimeGateway.Grpc.Tests;
using Orleans.Timers;

namespace DevTeam.Backend;

[TypeSubscription(SkillType.Sandbox)]
public sealed class Sandbox(
    [FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IManageAzure azureService,
    IReminderRegistry _reminderRegistry,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<Sandbox>>? logger = null)
    :
    BaseAgent(id, runtime, nameof(Sandbox), logger),
    IRemindable,
    IHandle<SandboxRunCreated>
{
    private SandboxMetadata sandboxState = new();
    private const string ReminderName = "SandboxRunReminder";
    private IGrainReminder? _reminder;

    public async ValueTask HandleAsync(SandboxRunCreated sandboxRunCreated, MessageContext messageContext)
    {
        var (org, repo, issueNumber, parentIssueNumber) = ExtractDetailsFromTopicSource(messageContext.Topic);

        // We save state so that the reminder can access it
        sandboxState.Org = org;
        sandboxState.Repo = repo;
        sandboxState.ParentIssueNumber = parentIssueNumber;
        sandboxState.IssueNumber = issueNumber;
        sandboxState.UserName = sandboxRunCreated.UserName;
        sandboxState.UserMessage = sandboxRunCreated.UserMessage;
        sandboxState.IsCompleted = false;

        await ScheduleCommitSandboxRun(org, repo, parentIssueNumber, issueNumber);
        await ValueTask.CompletedTask;
    }

    public async Task ScheduleCommitSandboxRun(string org, string repo, long askIssue, long issueNumber)
    {
        _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
            callingGrainId: this.GetGrainId(),
            reminderName: ReminderName,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMinutes(1));
    }

    async Task IRemindable.ReceiveReminder(string reminderName, TickStatus status)
    {
        if (!sandboxState.IsCompleted)
        {
            var sandboxId = $"sk-sandbox-{sandboxState.Org}-{sandboxState.Repo}-{sandboxState.ParentIssueNumber}-{sandboxState.IssueNumber}".ToUpperInvariant();

            if (await azureService.IsSandboxCompleted(sandboxId))
            {
                await azureService.DeleteSandbox(sandboxId);

                // Get the topic from the agent metadata
                var topics = agentsMetadata.GetTopicsForAgent(typeof(Dev));
                // TODO: How to handle multiple topics?
                var topic = topics?.FirstOrDefault() ?? SkillType.DevTeam;

                await PublishMessageAsync(new SandboxRunFinished
                {
                    UserName = sandboxState.UserName,
                    UserMessage = sandboxState.UserMessage,
                },
                topic: new TopicId(topic)
                ).ConfigureAwait(false);
                await Cleanup();
            }
        }
        else
        {
            await Cleanup();
        }
    }

    private async Task Cleanup()
    {
        sandboxState.IsCompleted = true;
        await _reminderRegistry.UnregisterReminder(
            this.GetGrainId(), _reminder);
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

    public override async ValueTask<JsonElement> SaveStateAsync()
    {
        SandboxMetadata sandboxMetadata = new()
        {
            Org = sandboxState.Org,
            Repo = sandboxState.Repo,
            ParentIssueNumber = sandboxState.ParentIssueNumber,
            IssueNumber = sandboxState.IssueNumber,
            IsCompleted = sandboxState.IsCompleted,
            UserName = sandboxState.UserName,
            UserMessage = sandboxState.UserMessage
        };

        return SerializedState.Create(sandboxMetadata).AsJson();
    }

    public override ValueTask LoadStateAsync(JsonElement state)
    {
        var sandboxMetadataLoaded = new SerializedState(state).As<SandboxMetadata>();

        sandboxState.Org = sandboxMetadataLoaded.Org;
        sandboxState.Repo = sandboxMetadataLoaded.Repo;
        sandboxState.ParentIssueNumber = sandboxMetadataLoaded.ParentIssueNumber;
        sandboxState.IssueNumber = sandboxMetadataLoaded.IssueNumber;
        sandboxState.IsCompleted = sandboxMetadataLoaded.IsCompleted;
        sandboxState.UserName = sandboxMetadataLoaded.UserName;
        sandboxState.UserMessage = sandboxMetadataLoaded.UserMessage;

        return ValueTask.CompletedTask;
    }
}

public class SandboxMetadata
{
    public string Org { get; set; } = default!;
    public string Repo { get; set; } = default!;
    public long ParentIssueNumber { get; set; }
    public long IssueNumber { get; set; }
    public bool IsCompleted { get; set; }
    public string UserName { get; set; } = default!;
    public string UserMessage { get; set; } = default!;
}
