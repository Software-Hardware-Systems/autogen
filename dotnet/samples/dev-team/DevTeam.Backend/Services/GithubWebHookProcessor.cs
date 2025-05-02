// Copyright (c) Microsoft Corporation. All rights reserved.
// GithubWebHookProcessor.cs

using System.Globalization;
using DevTeam.Backend.Agents.Developer;
using DevTeam.Backend.Agents.DeveloperLead;
using DevTeam.Backend.Agents.ProductManager;
using Google.Protobuf;
using Microsoft.AutoGen.Contracts;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Models;

namespace DevTeam.Backend.Services;

public sealed class GithubWebHookProcessor(ILogger<GithubWebHookProcessor> logger, IAgentRuntime agentRuntime) : WebhookEventProcessor
{
    private readonly ILogger<GithubWebHookProcessor> _logger = logger;
    private readonly IAgentRuntime _agentRuntime = agentRuntime;

    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(headers, nameof(headers));
            ArgumentNullException.ThrowIfNull(issuesEvent, nameof(issuesEvent));
            ArgumentNullException.ThrowIfNull(action, nameof(action));

            var org = issuesEvent.Repository?.Owner.Login ?? throw new InvalidOperationException("Repository owner login is null");
            var repo = issuesEvent.Repository?.Name ?? throw new InvalidOperationException("Repository name is null");
            var issueNumber = issuesEvent.Issue?.Number ?? throw new InvalidOperationException("Issue number is null");
            var userName = issuesEvent.Issue?.User.Name;
            var userAsk = issuesEvent.Issue?.Body ?? string.Empty;

            _logger.LogInformation($"{userName ?? "Somebody"} {(issuesEvent.Action == IssuesAction.Opened ? "Opened" : "Closed")} {org}-{repo}-{issueNumber} with Labels: {string.Join(",", issuesEvent.Issue?.Labels?.Select(l => l.Name) ?? Array.Empty<string>())}");

            // Note that we do process new issues even if the user is a bot

            // Assumes the label follows the following convention: Skill.Function example: PM.Readme
            // Also, we've introduced the Parent label, that ties the sub-issue with the parent issue
            var labels = issuesEvent.Issue?.Labels
                                    .Select(l => l.Name.Split('.'))
                                    .Where(parts => parts.Length == 2)
                                    .ToDictionary(parts => parts[0], parts => parts[1]);
            if (labels == null || labels.Count == 0)
            {
                _logger.LogWarning("No labels found in issue. Skip processing.");
                return;
            }

            // Use the first label with a Skill.Function format
            var skillType = labels.Keys.Where(k => k != "Parent").FirstOrDefault();
            if (skillType == null)
            {
                _logger.LogWarning("No skill type found in issue. Skip processing.");
                return;
            }

            // Create a unique topic source which when combined
            // with a topic type based on the skillType
            // results in a unique agent instance
            var topicSource = $"Org.{org}-Repo.{repo}-IssueNumber.{issueNumber.ToString()}";
            long? parentIssueNumber = labels.TryGetValue("Parent", out var value) ? long.Parse(value, CultureInfo.InvariantCulture) : null;
            if (parentIssueNumber != null)
            {
                topicSource += $"-ParentIssueNumber.{parentIssueNumber.ToString()}";
            }

            if (issuesEvent.Action == IssuesAction.Opened)
            {
                await HandleNewAsk(userName, userAsk, skillType, labels[skillType], topicSource);
            }
            else if (issuesEvent.Action == IssuesAction.Closed && issuesEvent.Issue?.User.Type.Value == UserType.Bot)
            {
                await HandleAskApproval(userName, userAsk, skillType, labels[skillType], topicSource);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing issue event");
            throw;
        }
    }

    protected override async Task ProcessIssueCommentWebhookAsync(
       WebhookHeaders headers,
       IssueCommentEvent issueCommentEvent,
       IssueCommentAction action)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(issueCommentEvent);
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            var org = issueCommentEvent.Repository!.Owner.Login;
            var repo = issueCommentEvent.Repository.Name;
            var issueNumber = issueCommentEvent.Issue.Number;
            var userName = issueCommentEvent.Comment.User.Name ?? issueCommentEvent.Comment.User.Login;
            var userComment = issueCommentEvent.Comment.Body;

            _logger.LogInformation($"{userName ?? "Somebody"} commented on {org}-{repo}-{issueNumber} with Labels: {string.Join(",", issueCommentEvent.Issue.Labels.Select(l => l.Name))}");

            // We skip processing if the comment is from a bot because
            // the bot creates comments to converse with the user
            if (issueCommentEvent.Sender!.Type.Value == UserType.Bot)
            {
                _logger.LogInformation("Bot comment. Skip processing");
                return;
            }

            // Assumes the label follows the following convention: Skill.Function example: PM.Readme
            var labels = issueCommentEvent.Issue.Labels
                                    .Select(l => l.Name.Split('.'))
                                    .Where(parts => parts.Length == 2)
                                    .ToDictionary(parts => parts[0], parts => parts[1]);
            if (labels == null || labels.Count == 0)
            {
                _logger.LogWarning("No labels found in issue. Skip processing.");
                return;
            }

            // Use the first label with a Skill.Function format
            var skillType = labels.Keys.Where(k => k != "Parent").FirstOrDefault();
            if (skillType == null)
            {
                _logger.LogWarning("No skill type found in issue. Skip processing.");
                return;
            }

            // Create a unique topic source which when combined
            // with a topic type based on the skillType
            // results in a unique agent instance
            var topicSource = $"Org.{org}-Repo.{repo}-IssueNumber.{issueNumber.ToString()}";
            long? parentIssueNumber = labels.TryGetValue("Parent", out var value) ? long.Parse(value, CultureInfo.InvariantCulture) : null;
            if (parentIssueNumber != null)
            {
                topicSource += $"-ParentIssueNumber.{parentIssueNumber.ToString()}";
            }

            await HandleNewAsk(userName, userComment, skillType, labels[skillType], topicSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing issue comment event");
            throw;
        }

    }

    private async Task HandleAskApproval(string? userName, string userMessage, string skillType, string skill, string topicSource)
    {
        try
        {
            _logger.LogInformation("Handling ask approval");

            IMessage askApprovalMessage = (skillType, skill) switch
            {
                (SkillType.ProductOwner, PMSkills.Readme) => new ReadmeIssueClosed { UserName = userName, UserMessage = userMessage },
                (SkillType.DeveloperLead, DeveloperLeadSkills.Plan) => new DevPlanIssueClosed { UserName = userName, UserMessage = userMessage },
                (SkillType.Developer, DeveloperSkills.Implement) => new CodeIssueClosed { UserName = userName, UserMessage = userMessage },
                _ => new CloudEvent() // TODO: default event
                                      // There is a bug in the agent message flow
                                      // Create a new issue explaining which skillName and functionName are not handled
                                      // Who/What handles a generic CloudEvent?
                                      // Can the CloudEvent be used to create a new issue?
            };

            await _agentRuntime.PublishMessageAsync(askApprovalMessage, new TopicId(skillType, topicSource));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handling ask approval");
            throw;
        }
    }

    private async Task HandleNewAsk(string? userName, string userMessage, string skillName, string functionName, string topicSource)
    {
        try
        {
            _logger.LogInformation("Handling new ask");

            IMessage newAskMessage = (skillName, functionName) switch
            {
                (SkillType.Stakeholder, StakeholderSkills.Ask) => new NewAsk { UserName = userName, UserMessage = userMessage },
                (SkillType.ProductOwner, PMSkills.Readme) => new ReadmeRequested { UserName = userName, UserMessage = userMessage },
                (SkillType.DeveloperLead, DeveloperLeadSkills.Plan) => new DevPlanRequested { UserName = userName, UserMessage = userMessage },
                (SkillType.Developer, DeveloperSkills.Implement) => new CodeGenerationRequested {UserName = userName, UserMessage = userMessage },
                _ => new CloudEvent()
                // If the issue already exists and we are responding to a comment
                // Reply with comment listing the available skill types and corresponding skills
            };

            // skill type is used as the typic type
            // Agent implementations subscribe to their corresponding topic type
            await _agentRuntime.PublishMessageAsync(newAskMessage, new TopicId(skillName, topicSource));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handling new ask");
            throw;
        }
    }
}
