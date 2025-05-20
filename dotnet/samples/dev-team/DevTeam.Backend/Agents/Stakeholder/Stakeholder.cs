// Copyright (c) Microsoft Corporation. All rights reserved.
// Stakeholder.cs

/*
 * The Stakeholder agent acts as a bridge between external business stakeholders 
 * and the SCRUM team's Product Owner. It translates business goals and requirements
 * into actionable items for the development team.
 * 
 * Key responsibilities:
 * - Process stakeholder requests (clarifications, reviews, approvals)
 * - Define and articulate business value propositions for features
 * - Assist with feature prioritization based on business value
 * - Facilitate structured feedback on sprint outcomes
 * - Maintain state information about business priorities and value
 * 
 * The agent integrates with GitHub through issue labels and comments,
 * allowing natural language interaction with human stakeholders.
 */

using DevTeam.Agents;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.AI;

namespace DevTeam.Backend.Agents.Stakeholder;

/*
 * The Stakeholder agent acts as a bridge between external business stakeholders 
 * and the SCRUM team's Product Owner. It translates business goals and requirements
 * into actionable items for the development team.
 * 
 * Key responsibilities:
 * - Process stakeholder requests (clarifications, reviews, approvals)
 * - Define and articulate business value propositions for features
 * - Assist with feature prioritization based on business value
 * - Facilitate structured feedback on sprint outcomes
 * - Maintain state information about business priorities and value
 * 
 * The agent integrates with GitHub through issue labels and comments,
 * allowing natural language interaction with human stakeholders.
 */
[TypeSubscription(SkillPersona.Stakeholder)]
public class Stakeholder(
    //[FromKeyedServices("AgentsMetadata")] AgentsMetadata agentsMetadata,
    IChatClient chatClient,
    //IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    //IVectorStore vectorStore,
    AgentId id,
    IAgentRuntime runtime,
    Logger<AiAgent<Stakeholder>>? logger = null)
    :
    AiAgent<Stakeholder>(chatClient, /*embeddingGenerator,*/ /*vectorStore,*/ id, runtime, logger),
    IHandle<StakeholderClarify>,
    IHandle<StakeholderAnswer>,
    IHandle<StakeholderReview>,
    IHandle<StakeholderApprove>,
    IHandle<StakeholderValueProposition>,
    IHandle<StakeholderPrioritization>,
    IHandle<StakeholderSprintFeedback>
{
    /// <summary>
    /// Extension to define stakeholder-specific label types
    /// </summary>
    public static class StakeholderSkills
    {
        public const string Clarify = "Clarify";
        public const string Answer = "Answer";
        public const string Review = "Review";
        public const string Approve = "Approve";
        public const string ValueProposition = "ValueProposition";
        public const string Prioritization = "Prioritization";
        public const string SprintFeedback = "SprintFeedback";
    }
    /// <summary>
    /// Defines available skills for the Stakeholder agent with descriptions
    /// </summary>
    protected override Dictionary<string, string> AvailableSkills => new()
    {
        { StakeholderSkills.Clarify, "Help clarify business requirements and intent" },
        { StakeholderSkills.Answer, "Provide answers to stakeholder questions" },
        { StakeholderSkills.Review, "Assist in reviewing deliverables from a business perspective" },
        { StakeholderSkills.Approve, "Process stakeholder approvals" },
        { StakeholderSkills.ValueProposition, "Help articulate business value propositions for features" },
        { StakeholderSkills.Prioritization, "Assist with feature prioritization based on business value" },
        { StakeholderSkills.SprintFeedback, "Facilitate structured feedback on sprint results" }
    };
    private static readonly Dictionary<Type, string> StakeholderPrompts = new()
    {
        { typeof(StakeholderClarify), StakeholderActivityPrompts.Clarify },
        { typeof(StakeholderAnswer), StakeholderActivityPrompts.Answer },
        { typeof(StakeholderReview), StakeholderActivityPrompts.Review },
        { typeof(StakeholderApprove), StakeholderActivityPrompts.Approve },
        { typeof(StakeholderValueProposition), StakeholderActivityPrompts.ValueProposition },
        { typeof(StakeholderPrioritization), StakeholderActivityPrompts.Prioritization },
        { typeof(StakeholderSprintFeedback), StakeholderActivityPrompts.SprintFeedback }
    };    // Method to get or create state for storing stakeholder-related information
    private StakeholderState _state = new StakeholderState();

    private Task<StakeholderState> GetOrCreateStateAsync()
    {
        // Return the existing state
        return Task.FromResult(_state);
    }

    protected sealed class StakeholderState
    {
        public AiAgentConversationState ConversationState { get; } = new();
        
        // Business priorities and goals
        public List<BusinessPriority> BusinessPriorities { get; set; } = new List<BusinessPriority>();
        
        // Value propositions for features
        public List<ValueProposition> ValuePropositions { get; set; } = new List<ValueProposition>();
        
        // Feature prioritization details
        public List<FeaturePriority> FeaturePriorities { get; set; } = new List<FeaturePriority>();
        
        // History of stakeholder feedback
        public List<StakeholderFeedback> FeedbackHistory { get; set; } = new List<StakeholderFeedback>();
        
        // Metrics and KPIs the stakeholder cares about
        public List<StakeholderMetric> Metrics { get; set; } = new List<StakeholderMetric>();
    }
    
    // Models for state tracking
    protected class BusinessPriority
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Importance { get; set; } // 1-10 scale
        public string TargetDate { get; set; } = string.Empty;
        public List<string> RelatedFeatures { get; set; } = new();
    }
    
    protected class ValueProposition
    {
        public string FeatureName { get; set; } = string.Empty;
        public string CustomerSegment { get; set; } = string.Empty;
        public string ValueStatement { get; set; } = string.Empty;
        public List<string> Benefits { get; set; } = new();
        public List<string> Risks { get; set; } = new();
    }
    
    protected class FeaturePriority
    {
        public string FeatureName { get; set; } = string.Empty;
        public int CustomerImpact { get; set; } // 1-10 scale
        public int DevelopmentEffort { get; set; } // 1-10 scale
        public int BusinessValue { get; set; } // 1-10 scale
        public int Risk { get; set; } // 1-10 scale
        public List<string> Dependencies { get; set; } = new();
        public string OptimalSprint { get; set; } = string.Empty;
    }
    
    protected class StakeholderFeedback
    {
        public string SprintOrFeature { get; set; } = string.Empty;
        public string FeedbackContent { get; set; } = string.Empty;
        public bool AlignedWithGoals { get; set; }
        public List<string> ActionItems { get; set; } = new();
        public string Timestamp { get; set; } = string.Empty;
    }
    
    protected class StakeholderMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CurrentValue { get; set; } = string.Empty;
        public string TargetValue { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public bool IsKpi { get; set; }
    }

    public async ValueTask HandleAsync(StakeholderClarify stakeholderClarify, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse(stakeholderClarify, stakeholderClarify.UserName, stakeholderClarify.UserMessage);
        
        // Create a more detailed response with understanding and questions
        var clarificationQuestions = new List<string>
        {
            "Have you considered how this might impact existing features?",
            "Are there specific metrics you'd like to track for this request?",
            "What timeline constraints should we be aware of?"
        };
        
        await PublishMessageAsync(
            new StakeholderClarified 
            { 
                Response = response,
                Understanding = "I understand your request is about " + stakeholderClarify.Context,
                ClarificationQuestions = { clarificationQuestions },
                MissingAspects = { "Timeline constraints", "Success metrics", "Priority relative to other features" },
                ProactiveVerificationQuestion = "Would you like me to analyze potential impacts on existing workflows?"
            },
            topic: new TopicId(SkillPersona.Hubber, messageContext.Topic!.Value.Source)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(StakeholderAnswer answer, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse(answer, answer.UserName, answer.UserMessage);
          // Track if we need follow-up based on content analysis (simplified here)
        bool requiresFollowUp = !string.IsNullOrEmpty(answer.Context) && answer.Context.Contains('?');
        
        await PublishMessageAsync(
            new StakeholderAnswered 
            { 
                Response = response,
                RequiresFollowUp = requiresFollowUp,
                FollowUpQuestion = requiresFollowUp ? "Could you provide more details about the target users?" : string.Empty,
                Status = "Complete",
                ProactiveVerificationQuestion = "Would you like to see how this answer might affect the project timeline?"
            },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Hubber)
        ).ConfigureAwait(false);
    }    public async ValueTask HandleAsync(StakeholderReview review, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse(review, review.UserName, review.UserMessage);
          // Track review in state
        var state = await GetOrCreateStateAsync().ConfigureAwait(false);
        state.FeedbackHistory.Add(new StakeholderFeedback
        {
            SprintOrFeature = review.ItemReviewed,
            FeedbackContent = review.UserMessage,
            Timestamp = DateTime.UtcNow.ToString("o"),
            AlignedWithGoals = !review.UserMessage.Contains("not aligned", StringComparison.OrdinalIgnoreCase) && !review.UserMessage.Contains("misaligned", StringComparison.OrdinalIgnoreCase)
        });
        
        // Simple approval logic - could be more sophisticated in production
        bool approved = !review.UserMessage.Contains("not approved", StringComparison.OrdinalIgnoreCase) && 
                        !review.UserMessage.Contains("reject", StringComparison.OrdinalIgnoreCase) &&
                        !review.UserMessage.Contains("disapprove", StringComparison.OrdinalIgnoreCase);
        
        await PublishMessageAsync(
            new StakeholderReviewed 
            { 
                Response = response,
                Approved = approved,
                ChangesRequested = { "Improve user experience", "Add more detailed metrics" },
                NextSteps = "Update the implementation plan based on feedback",
                ProactiveVerificationQuestion = "Should we verify if these changes align with our original business goals?"
            },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Stakeholder)
        ).ConfigureAwait(false);
    }

    public async ValueTask HandleAsync(StakeholderApprove approve, MessageContext messageContext)
    {
        var response = await GenerateStakeholderResponse(approve, approve.UserName, approve.UserMessage);
        
        await PublishMessageAsync(
            new ResponseToStakeholder 
            { 
                Response = response,
                ActionTaken = $"Approved {approve.ItemApproved}",
                NextSteps = { "Update product backlog", "Prioritize for next sprint", "Inform development team" },
                NextInteractionSuggestion = "You might want to review the implementation plan next",
                ChangeRequests = { approve.ChangeRequests.ToList() },
                IsApproved = !approve.WithChanges || (approve.ChangeRequests.Count == 0)
            },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.Stakeholder)
        ).ConfigureAwait(false);
    }
      public async ValueTask HandleAsync(StakeholderValueProposition valueProposition, MessageContext messageContext)
    {
        // Generate response using the value proposition prompt
        var response = await GenerateStakeholderResponse(
            valueProposition, 
            valueProposition.UserName, 
            $"Feature: {valueProposition.FeatureName}\nCustomer Segment: {valueProposition.CustomerSegment}\nDescription: {valueProposition.Description}"
        );
          // Track the value proposition in state
        var state = await GetOrCreateStateAsync().ConfigureAwait(false);
        state.ValuePropositions.Add(new ValueProposition
        {
            FeatureName = valueProposition.FeatureName,
            CustomerSegment = valueProposition.CustomerSegment,
            ValueStatement = valueProposition.Description,
            Benefits = valueProposition.ExpectedBenefits.ToList(),
            Risks = new List<string>() // Would be populated based on AI analysis
        });
        
        // Create user stories based on the value proposition (simplified example)
        var userStories = new List<string>
        {
            $"As a {valueProposition.CustomerSegment}, I want {valueProposition.FeatureName} so that I can {valueProposition.ExpectedBenefits.FirstOrDefault() ?? "achieve the expected benefit"}.",
            $"As a product owner, I want {valueProposition.FeatureName} to meet performance metrics so that we can demonstrate {valueProposition.ExpectedBenefits.LastOrDefault() ?? "business value"}."
        };
        
        // Suggest metrics based on the value proposition
        var suggestedMetrics = new List<string>
        {
            "User adoption rate",
            "Time to complete task",
            "Customer satisfaction score",
            "Revenue impact"
        };
        
        await PublishMessageAsync(
            new ValuePropositionProcessed 
            { 
                Response = response,
                FormalizedValueProposition = $"This feature provides {string.Join(", ", valueProposition.ExpectedBenefits)} to {valueProposition.CustomerSegment} by implementing {valueProposition.FeatureName}.",
                RelatedUserStories = { userStories },
                SuggestedMetrics = { suggestedMetrics },
                NextSteps = "Present to Product Owner for backlog prioritization"
            },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.ProductOwner)
        ).ConfigureAwait(false);
    }
      public async ValueTask HandleAsync(StakeholderPrioritization prioritization, MessageContext messageContext)
    {
        // Generate response using the prioritization prompt
        var response = await GenerateStakeholderResponse(
            prioritization, 
            prioritization.UserName, 
            $"Prioritization context: {prioritization.PrioritizationContext}\nTimeline constraints: {prioritization.TimelineConstraints}"
        );
          // Track prioritization in state
        var state = await GetOrCreateStateAsync().ConfigureAwait(false);
        foreach (var feature in prioritization.Features)
        {
            state.FeaturePriorities.Add(new FeaturePriority
            {
                FeatureName = feature.FeatureName,
                BusinessValue = feature.BusinessValue,
                // Other fields would be populated based on additional input or AI analysis
            });
        }
        
        // Create backlog summary
        string backlogSummary = "Updated backlog priorities:\n";
        foreach (var feature in prioritization.Features.OrderBy(f => f.RelativePriority))
        {
            backlogSummary += $"- {feature.FeatureName}: Priority {feature.RelativePriority}, Business Value: {feature.BusinessValue}/10\n";
        }
        
        // Analyze impacts on timeline
        var timelineImpacts = new List<string>
        {
            $"Highest priority items can be completed in upcoming sprint",
            $"Lower priority items may need to be deferred by 1-2 sprints",
            $"Overall timeline aligns with {prioritization.TimelineConstraints}"
        };
        
        await PublishMessageAsync(
            new PrioritizationProcessed 
            { 
                Response = response,
                UpdatedBacklogSummary = backlogSummary,
                ImpactOnTimeline = { timelineImpacts },
                PotentialConflicts = { "Resource constraints for items 2 and 3", "Dependency on external API for item 1" },
                Recommendation = "Proceed with top 3 priorities for next sprint planning"
            },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.ProductOwner)
        ).ConfigureAwait(false);
    }
      public async ValueTask HandleAsync(StakeholderSprintFeedback sprintFeedback, MessageContext messageContext)
    {
        // Generate response using the sprint feedback prompt
        var response = await GenerateStakeholderResponse(
            sprintFeedback, 
            sprintFeedback.UserName, 
            $"Sprint ID: {sprintFeedback.SprintId}\nOverall assessment: {sprintFeedback.OverallAssessment}"
        );
        
        // Track feedback in state
        var state = await GetOrCreateStateAsync().ConfigureAwait(false);
        state.FeedbackHistory.Add(new StakeholderFeedback
        {
            SprintOrFeature = sprintFeedback.SprintId,
            FeedbackContent = sprintFeedback.OverallAssessment,
            Timestamp = DateTime.UtcNow.ToString("o"),
            AlignedWithGoals = !sprintFeedback.OverallAssessment.Contains("not aligned")
        });
        
        // Create feedback summary
        string feedbackSummary = $"Sprint {sprintFeedback.SprintId} feedback summary:\n{sprintFeedback.OverallAssessment}\n\nFeature feedback:";
        foreach (var feature in sprintFeedback.CompletedFeatures)
        {
            feedbackSummary += $"\n- {feature.FeatureName}: {(feature.MeetsExpectations ? "Meets expectations" : "Needs improvement")}";
        }
        
        // Analyze actionable items
        var actionableItems = new List<string>();
        foreach (var feature in sprintFeedback.CompletedFeatures.Where(f => !f.MeetsExpectations))
        {
            actionableItems.AddRange(feature.SuggestedImprovements);
        }
        
        await PublishMessageAsync(
            new SprintFeedbackProcessed 
            { 
                Response = response,
                FeedbackSummary = feedbackSummary,
                ActionableItems = { actionableItems },
                BacklogImplications = { "Re-prioritize items based on feedback", "Add improvement tasks to backlog" },
                NextSprintRecommendations = "Focus on addressing feedback while maintaining momentum on priority features"
            },
            topic: messageContext.Topic ?? new TopicId(SkillPersona.ProductOwner)
        ).ConfigureAwait(false);
    }

    private async Task<string> GenerateStakeholderResponse(object message, string authorName, string authorAsk)
    {
        try
        {
            var messageType = message.GetType();
            if (!StakeholderPrompts.TryGetValue(messageType, out var systemPrompt))
            {
                throw new ArgumentOutOfRangeException(nameof(message), $"No prompt defined for message type {messageType.Name}");
            }            // For context enrichment - would implement in production
            var state = await GetOrCreateStateAsync().ConfigureAwait(false);
            string businessContextInfo = string.Empty;
            
            // Build business context based on state
            if (state.BusinessPriorities.Any())
            {
                businessContextInfo += "\nBusiness Priorities:\n";
                foreach (var priority in state.BusinessPriorities.OrderByDescending(p => p.Importance).Take(3))
                {
                    businessContextInfo += $"- {priority.Name}: {priority.Description} (Importance: {priority.Importance}/10)\n";
                }
            }
            
            if (state.ValuePropositions.Any())
            {
                businessContextInfo += "\nValue Propositions:\n";
                foreach (var vp in state.ValuePropositions.Take(2))
                {
                    businessContextInfo += $"- {vp.FeatureName}: {vp.ValueStatement}\n";
                }
            }
            
            // In a complete implementation, we would inject the context into the prompt
            // taskSpecificInstructions = $"Consider the following additional knowledge when generating your response:";
            // knowledgeCollection = businessContextInfo;
            // await AddKnowledgeInstructions(taskSpecificInstructions, knowledgeCollection);

            logger?.LogDebug("Generating response for activity type: {ActivityType}", messageType.Name);

            return await GenerateResponseUsing(systemPrompt, authorName, authorAsk);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error handling stakeholder activity: {ActivityType}", message.GetType().Name);
            return "";
        }
    }
}
