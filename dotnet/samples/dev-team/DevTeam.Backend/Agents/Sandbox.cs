// Copyright (c) Microsoft Corporation. All rights reserved.
// Sandbox.cs

using DevTeam.Agents;
using DevTeam.Backend.Services;
using Microsoft.AutoGen.Contracts;
using Microsoft.SemanticKernel.Memory;
using Orleans.Timers;

namespace DevTeam.Backend;

public sealed class Sandbox(
    IManageAzure azureService,
    IPersistentState<SandboxMetadata> state,
    IReminderRegistry _reminderRegistry,
    ISemanticTextMemory semanticTextMemory,
    AutoGen.Core.IAgent coreAgent,
    IHostApplicationLifetime hostApplicationLifetime,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<Sandbox>>? logger = null) :
    AiAgent<Sandbox>(semanticTextMemory, coreAgent, hostApplicationLifetime, id, runtime, logger),
    IRemindable,
    IHandle<SandboxRunCreated>

{
    private const string ReminderName = "SandboxRunReminder";
    private IGrainReminder? _reminder;

    public async ValueTask HandleAsync(SandboxRunCreated item, MessageContext messageContext)
    {
        await ScheduleCommitSandboxRun(state.State.Org, state.State.Repo, TryParseLong(state.State.ParentIssueNumber), TryParseLong(state.State.IssueNumber));
        await ValueTask.CompletedTask;
    }
    public async Task ScheduleCommitSandboxRun(string org, string repo, long parentIssueNumber, long issueNumber)
    {
        await StoreState(org, repo, parentIssueNumber, issueNumber);
        _reminder = await _reminderRegistry.RegisterOrUpdateReminder(
            callingGrainId: this.GetGrainId(),
            reminderName: ReminderName,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromMinutes(1));
    }

    async Task IRemindable.ReceiveReminder(string reminderName, TickStatus status)
    {
        if (!state.State.IsCompleted)
        {
            var sandboxId = $"sk-sandbox-{state.State.Org}-{state.State.Repo}-{state.State.ParentIssueNumber}-{state.State.IssueNumber}".ToUpperInvariant();

            if (await azureService.IsSandboxCompleted(sandboxId))
            {
                await azureService.DeleteSandbox(sandboxId);
                await PublishMessageAsync(new SandboxRunFinished
                {
                    UserId = state.State.UserId,
                    UserMessage = state.State.UserMessage,
                },
                topic: new TopicId(Consts.TopicName));
                await Cleanup();
            }
        }
        else
        {
            await Cleanup();
        }
    }

    private async Task StoreState(string org, string repo, long parentIssueNumber, long issueNumber)
    {
        state.State.Org = org;
        state.State.Repo = repo;
        state.State.ParentIssueNumber = parentIssueNumber;
        state.State.IssueNumber = issueNumber;
        state.State.IsCompleted = false;
        await state.WriteStateAsync();
    }

    private async Task Cleanup()
    {
        state.State.IsCompleted = true;
        await _reminderRegistry.UnregisterReminder(
            this.GetGrainId(), _reminder);
        await state.WriteStateAsync();
    }

    private long TryParseLong(object value)
    {
        return long.TryParse(value?.ToString(), out var result) ? result : 0;
    }
}

public class SandboxMetadata
{
    public string Org { get; set; } = default!;
    public string Repo { get; set; } = default!;
    public long ParentIssueNumber { get; set; }
    public long IssueNumber { get; set; }
    public bool IsCompleted { get; set; }
    public string UserId { get; set; } = default!;
    public string UserMessage { get; set; } = default!;
}
