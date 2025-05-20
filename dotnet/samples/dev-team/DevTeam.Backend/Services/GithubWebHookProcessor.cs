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
using static DevTeam.Backend.Agents.Stakeholder.Stakeholder;

namespace DevTeam.Backend.Services;

public sealed class GithubWebHookProcessor : WebhookEventProcessor
{
    private readonly ILogger<GithubWebHookProcessor> _logger;
    private readonly IAgentRuntime _agentRuntime;

    public GithubWebHookProcessor(ILogger<GithubWebHookProcessor> logger, IAgentRuntime agentRuntime)
    {
        _logger = logger;
        _agentRuntime = agentRuntime;
    }

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
            var userName = issuesEvent.Issue.User.Name ?? issuesEvent.Issue.User.Login ?? issuesEvent.Organization!.Login;
            var issueContent = issuesEvent.Issue?.Body ?? string.Empty;

            _logger.LogInformation($"{issuesEvent.Sender!.Type.Value} {userName ?? "Somebody"} {issuesEvent.Action} {org}-{repo}-{issueNumber} {string.Join(",", issuesEvent.Issue?.Labels?.Select(l => l.Name) ?? Array.Empty<string>())}");            // Note that we do process new issues even if the user is a bot

            // Assumes the label follows the following convention: Skill.Function example: PM.Readme
            // Also, we've introduced the Parent label, that ties the sub-issue with the parent issue
            var labels = issuesEvent.Issue?.Labels
                                    .Select(l => l.Name.Split('.'))
                                    .Where(parts => parts.Length == 2)
                                    .ToDictionary(parts => parts[0], parts => parts[1]);
            
            string? skillType = null;
            string? skillActivity = null;
            
            // Check if we have explicit labels
            if (labels != null && labels.Count > 0)
            {
                // Use the first label with a Skill.Function format
                skillType = labels.Keys.Where(k => k != "Parent").FirstOrDefault();
                if (skillType != null)
                {
                    skillActivity = labels[skillType];
                }
            }
            // If no explicit skill label was found, automatically infer it
            if (skillType == null || skillActivity == null)
            {
                _logger.LogInformation("No explicit skill label found. Using automatic skill inference.");
                
                // Use Stakeholder agent by default for unlabeled issues
                skillType = SkillPersona.Stakeholder;
                
                // We'll determine the specific skill activity using the AI agent's inference
                // For now, assume Clarify as default, but this will be overridden by the agent
                skillActivity = StakeholderSkills.Clarify;
            }

            // Create a unique topic source which when combined
            // with a topic type based on the skillType
            // results in a unique agent instance
            var topicSource = $"Org={org}|Repo={repo}|IssueNumber={issueNumber}";
            
            // Only try to get parent issue number if we have labels
            if (labels != null && labels.Count > 0 && labels.TryGetValue("Parent", out var value))
            {
                long? parentIssueNumber = long.Parse(value, CultureInfo.InvariantCulture);
                topicSource += $"|ParentIssueNumber={parentIssueNumber}";
            }

            switch (action)
            {
                case var ia when ia == IssuesAction.Opened:
                    await HandleNewAsk(userName, issueContent, skillType, skillActivity, topicSource);
                    break;
                case var ia when ia == IssuesAction.Closed:
                    await HandleAskApproval(repo, userName, issueContent, skillType, skillActivity, topicSource);
                    break;
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
            var userName = issueCommentEvent.Comment.User.Name ?? issueCommentEvent.Comment.User.Login ?? issueCommentEvent.Organization!.Login;
            var userComment = issueCommentEvent.Comment.Body;

            _logger.LogInformation($"{issueCommentEvent.Sender!.Type.Value} {userName ?? "Somebody"} {action} {org}-{repo}-{issueNumber} {string.Join(",", issueCommentEvent.Issue.Labels.Select(l => l.Name))}");

            // We skip processing if the comment is from a bot because
            // the bot creates comments to converse with the user
            if (issueCommentEvent.Sender!.Type.Value == UserType.Bot)
            {
                _logger.LogInformation("Bot comment. Skip processing");
                return;
            }

            // Assumes the label follows the following convention: Skill.Function example: PM.Readme
            // Also, we've introduced the Parent label, that ties the sub-issue with the parent issue
            var labels = issueCommentEvent.Issue.Labels
                                    .Select(l => l.Name.Split('.'))
                                    .Where(parts => parts.Length == 2)
                                    .ToDictionary(parts => parts[0], parts => parts[1]);
            
            string? skillType = null;
            string? skillActivity = null;
            
            // Check if we have explicit labels
            if (labels != null && labels.Count > 0)
            {
                // Use the first label with a Skill.Function format
                skillType = labels.Keys.Where(k => k != "Parent").FirstOrDefault();
                if (skillType != null)
                {
                    skillActivity = labels[skillType];
                }
            }

            // If no explicit skill label was found, automatically infer it
            if (skillType == null || skillActivity == null)
            {
                _logger.LogInformation("No explicit skill label found for comment event. Using automatic skill inference.");
                
                // Use Stakeholder agent by default for unlabeled issues
                skillType = SkillPersona.Stakeholder;
                
                // We'll determine the specific skill activity using the AI agent's inference
                // For now, assume Clarify as default, but this will be overridden by the agent
                skillActivity = StakeholderSkills.Clarify;
            }

            // Create a unique topic source which when combined
            // with a topic type based on the skillType
            // results in a unique agent instance
            var topicSource = $"Org={org}|Repo={repo}|IssueNumber={issueNumber}";
            
            // Only try to get parent issue number if we have labels
            // This check should use the original 'labels' dictionary from the issue
            if (labels != null && labels.Count > 0 && labels.TryGetValue("Parent", out var value))
            {
                long? parentIssueNumber = long.Parse(value, CultureInfo.InvariantCulture);
                topicSource += $"|ParentIssueNumber={parentIssueNumber}";
            }

            // Currently, all non-bot comment actions (created, edited, deleted) on an issue with determinable skills
            // will trigger HandleNewAsk. This behavior is maintained.
            // If specific actions like only 'IssueCommentAction.Created' should be processed,
            // a switch statement or if condition on 'action' would be needed here.
            // For example: if (action == IssueCommentAction.Created) { ... }
            await HandleNewAsk(userName, userComment, skillType, skillActivity, topicSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing issue comment event");
            throw;
        }
    }

    private async Task HandleAskApproval(string? projectName, string? userName, string userMessage, string skillPersona, string skillActivity, string topicSource)
    {
        try
        {
            _logger.LogInformation("Handling ask approval");

            // Extract meaningful approval context from the user message
            // Strip any prefixes that might interfere with analysis
            string cleanUserMessage = userMessage;

            // The approval context will be the cleaned user message
            // In a future enhancement, this could fetch the latest comment on the issue
            // instead of using the closing message if it doesn't contain sufficient context
            string approvalContextMessage = cleanUserMessage;

            IMessage askApprovalMessage = (skillPersona, skillActivity) switch
            {
                // Handle stakeholder approvals based on activity type
                (SkillPersona.Stakeholder, StakeholderSkills.Clarify) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Clarification",
                    ProjectName = projectName ?? "SWHWSysDevTeam",
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Answer) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Answer",
                    ProjectName = projectName ?? "SWHWSysDevTeam",
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Review) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Review",
                    ProjectName = projectName ?? "SWHWSysDevTeam"
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Approve) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Approval",
                    ProjectName = projectName ?? "SWHWSysDevTeam"
                },
                (SkillPersona.Stakeholder, StakeholderSkills.ValueProposition) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Value Proposition",
                    ProjectName = projectName ?? "SWHWSysDevTeam"
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Prioritization) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Prioritization",
                    ProjectName = projectName ?? "SWHWSysDevTeam"
                },
                (SkillPersona.Stakeholder, StakeholderSkills.SprintFeedback) => new StakeholderApprove
                {
                    UserName = userName,
                    UserMessage = approvalContextMessage,
                    ItemApproved = "Sprint Feedback",
                    ProjectName = projectName ?? "SWHWSysDevTeam"
                },

                // Standard SCRUM team approvals
                (SkillPersona.ProductOwner, nameof(PMSkills.Readme)) => new ReadmeIssueClosed { UserName = userName, UserMessage = userMessage },
                (SkillPersona.DeveloperLead, nameof(DeveloperLeadSkills.Plan)) => new DevPlanIssueClosed { UserName = userName, UserMessage = userMessage },
                (SkillPersona.Developer, nameof(DeveloperSkills.Implement)) => new CodeIssueClosed { UserName = userName, UserMessage = userMessage },
                _ => new CloudEvent() // TODO: default event
                                      // There is a bug in the agent message flow
                                      // Create a new issue explaining which skillName and functionName are not handled
                                      // Who/What handles a generic CloudEvent?
                                      // Can the CloudEvent be used to create a new issue?
            };

            await _agentRuntime.PublishMessageAsync(askApprovalMessage, new TopicId(skillPersona, topicSource));

            _logger.LogDebug($"Published approval message type {askApprovalMessage.GetType().Name} to topic {skillPersona}/{topicSource}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handling ask approval");
            throw;
        }
    }    private async Task HandleNewAsk(string? userName, string userMessage, string skillPersona, string skillActivity, string topicSource)
    {
        try
        {
            _logger.LogDebug($"Handling new ask from {userName} to {skillPersona}.{skillActivity} about {topicSource}");
            _logger.LogTrace($"User message: {userMessage}");
            
            // For automatic skill inference, we'll include the original message
            // without skill prefix and let the agent infer it internally
            bool useInference = false;
            
            // If we're using the Stakeholder agent without a specific label or skill indicator
            if (skillPersona == SkillPersona.Stakeholder && !userMessage.Contains('['))
            {
                _logger.LogInformation("Using automatic skill inference for unlabeled message");
                useInference = true;
            }
            
            // Handle stakeholder interactions as well as standard SCRUM team interactions
            IMessage newAskMessage = (skillPersona, skillActivity) switch
            {
                // All stakeholder interactions handled with existing message types
                // We differentiate the activity type in the UserMessage with a prefix
                // If useInference is true, pass the message without skill prefix to allow internal inference
                (SkillPersona.Stakeholder, StakeholderSkills.Clarify) => new StakeholderClarify { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[CLARIFY] {userMessage}"
                },                (SkillPersona.Stakeholder, StakeholderSkills.Answer) => new StakeholderAnswer { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[ANSWER] {userMessage}"
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Review) => new StakeholderReview { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[REVIEW] {userMessage}" 
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Approve) => new StakeholderApprove { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[APPROVE] {userMessage}" 
                },
                // Enhanced stakeholder skills
                (SkillPersona.Stakeholder, StakeholderSkills.ValueProposition) => new StakeholderValueProposition { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[VALUE_PROPOSITION] {userMessage}"
                    // Feature name and business value will be extracted from the message by the Stakeholder agent
                },
                (SkillPersona.Stakeholder, StakeholderSkills.Prioritization) => new StakeholderPrioritization { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[PRIORITIZATION] {userMessage}"
                    // Prioritized features will be extracted from the message by the Stakeholder agent
                },
                (SkillPersona.Stakeholder, StakeholderSkills.SprintFeedback) => new StakeholderSprintFeedback { 
                    UserName = userName, 
                    UserMessage = useInference ? userMessage : $"[SPRINT_FEEDBACK] {userMessage}"
                    // Sprint ID and feature feedback will be extracted from the message by the Stakeholder agent
                },
                
                // Standard SCRUM team interactions
                (SkillPersona.ProductOwner, nameof(PMSkills.Readme)) => new ReadmeRequested { UserName = userName, UserMessage = userMessage },
                (SkillPersona.DeveloperLead, nameof(DeveloperLeadSkills.Plan)) => new DevPlanRequested { UserName = userName, UserMessage = userMessage },
                (SkillPersona.Developer, nameof(DeveloperSkills.Implement)) => new CodeGenerationRequested { UserName = userName, UserMessage = userMessage },
                  _ => new CloudEvent()
                // If the issue already exists and we are responding to a comment
                // Reply with comment listing the available skill types and corresponding skills
            };

            // skill type is used as the topic type
            // Agent implementations subscribe to their corresponding topic type
            await _agentRuntime.PublishMessageAsync(newAskMessage, new TopicId(skillPersona, topicSource));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handling new ask");
            throw;
        }
    }
}
