// Copyright (c) Microsoft Corporation. All rights reserved.
// AiAgent.cs

using System.ComponentModel;
using System.Text.Json;
using Microsoft.AutoGen.AgentChat.State;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.AI;
//using Microsoft.Extensions.VectorData;

namespace DevTeam.Agents;

public class AiAgent<T> : BaseAgent
{
    public AiAgent(
        IChatClient chatClient,
        //IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        //IVectorStore vectorStore,
        AgentId id,
        IAgentRuntime runtime,
        ILogger<AiAgent<T>>? logger = null)
        :
        base(id, runtime, nameof(AiAgent<T>), logger)
    {
        //_embeddingGenerator = embeddingGenerator;
        //_vectorStore = vectorStore;
        _chatClient = chatClient;
        _chatOptions = new() { Tools = [AIFunctionFactory.Create(RetrieveAdditionalKnowledge), AIFunctionFactory.Create(ClassifySkill)] };
    }

    //private IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    //private IVectorStore _vectorStore;
    private IChatClient _chatClient;
    private ChatOptions _chatOptions;

    protected AiAgentConversationState ConversationState { get; } = new();

    /// <summary>
    /// Dictionary of available skills for the agent. Can be overridden by derived classes.
    /// </summary>
    protected virtual Dictionary<string, string> AvailableSkills => new();

    /// <summary>
    /// Represents the state of an AI agent instance, including a list of chat messages, knowledge instructions, and
    /// generated responses.
    /// </summary>
    protected sealed class AiAgentConversationState
    {
        public List<string> KnowledgeInstructions { get; } = [];
        public List<ChatMessage> UserAsks { get; } = [];
        public List<ChatResponse> Generations { get; } = [];
        public void AddGeneration(ChatMessage userAsk, ChatResponse generation)
        {
            UserAsks.Add(userAsk);
            Generations.Add(generation);
        }
        public string GetLastGeneration()
        {
            var lastChatResponse = Generations.LastOrDefault();
            return lastChatResponse?.Messages.FirstOrDefault()?.Text ?? string.Empty;
        }
    }

    /// <summary>
    /// Replace the BaseAgent SaveStateAsync method to save the state of the agent.
    /// </summary>
    /// <returns><see cref="ValueTask<JsonElement>"></returns>
    public override async ValueTask<JsonElement> SaveStateAsync()
    {
        AiAgentConversationState aiAgentSaved = new AiAgentConversationState();

        aiAgentSaved.UserAsks.AddRange(ConversationState.UserAsks.ToList());
        aiAgentSaved.KnowledgeInstructions.AddRange(ConversationState.KnowledgeInstructions.ToList());
        aiAgentSaved.Generations.AddRange(ConversationState.Generations.ToList());

        return SerializedState.Create(aiAgentSaved).AsJson();
    }

    /// <summary>
    /// Replace the BaseAgent LoadStateAsync
    /// </summary>
    /// <param name="state"></param>
    public override ValueTask LoadStateAsync(JsonElement state)
    {
        var aiAgentLoaded = new SerializedState(state).As<AiAgentConversationState>();

        ConversationState.UserAsks.Clear();
        ConversationState.UserAsks.AddRange(aiAgentLoaded.UserAsks.ToList());
        ConversationState.KnowledgeInstructions.Clear();
        ConversationState.KnowledgeInstructions.AddRange(aiAgentLoaded.KnowledgeInstructions.ToList());
        ConversationState.Generations.Clear();
        ConversationState.Generations.AddRange(aiAgentLoaded.Generations.ToList());

        return ValueTask.CompletedTask;
    }

    protected async Task AddKnowledgeInstructions(string instruction, string knowledgeCollection)
    {
        ConversationState.KnowledgeInstructions.Add($"{instruction}: {knowledgeCollection}");

        // ToDo: Add code similar to the Seed project that creates the knowledge collection data
        // This may be a candidate task for MagenticOne
        // This will entail:
        // Understanding the knowledgeCollection argument
        // Searching the file system or web for the corresponding document information
        // Parsing/Chunking the information
        // Encoding the information into a vector memory
        // Periodically checking for updated information and encoding it
    }

    /// <summary>
    /// A tool made available during ChatClient inference
    /// </summary>
    /// <param name="knowledgeCollection">Specifies the source of knowledge to search for relevant information.</param>
    /// <param name="input">Defines the query or context for which additional knowledge is being sought.</param>
    /// <param name="limit">Sets the maximum number of knowledge items to retrieve from the collection.</param>
    /// <returns>Returns a string containing the relevant knowledge or a message indicating its absence.</returns>
    [Description("Retrieves additional knowledge based on the provided input from a specified collection")]
    private async Task<string> RetrieveAdditionalKnowledge(string knowledgeCollection, string input, int limit = 5)
    {
        return "No additional knowledge is available";
    }

    /// <summary>
    /// Classifies a user message into an appropriate skill type for agent response
    /// </summary>
    /// <param name="message">The user message to classify</param>
    /// <returns>The skill type that best matches the user's intent</returns>
    [Description("Determines the appropriate skill based on the content of the user message")]
    private async Task<string> ClassifySkill(string message)
    {
        if (AvailableSkills.Count == 0)
        {
            return "Unknown";
        }

        // Check for explicit skill indicators in the message
        // (e.g., "[CLARIFY]", "[REVIEW]", etc.)
        foreach (var skill in AvailableSkills)
        {
            if (message.Contains($"[{skill.Key}]", StringComparison.OrdinalIgnoreCase))
            {
                return skill.Key;
            }
        }

        // Prepare a prompt for the LLM to classify the skill
        string skillClassificationPrompt = $@"
Classify the following message into one of these skill types: {string.Join(", ", AvailableSkills.Keys)}.

For reference, here's what each skill type is for:
{string.Join("\n", AvailableSkills.Select(s => $"- {s.Key}: {s.Value}"))}

Analyze the user's intent and respond with just the name of the most appropriate skill type.
Message to classify: {message}

The skill type is:";

        ChatMessage systemMessage = new ChatMessage(ChatRole.System, skillClassificationPrompt);
        List<ChatMessage> messages = [systemMessage];
        
        _logger?.LogDebug("Classifying skill for message: {MessagePreview}", message.Length > 50 ? string.Concat(message.AsSpan(0, 50), "...") : message);
        
        ChatResponse response = await _chatClient.GetResponseAsync(messages, new ChatOptions { Temperature = 0.0f });
        
        string skillType = response.Text.Trim();
        
        // Validate that the returned skill is in our available skills
        if (!AvailableSkills.ContainsKey(skillType))
        {
            _logger?.LogWarning("Skill classification returned unknown skill: {Skill}. Using default.", skillType);
            // Return the first skill as default
            return AvailableSkills.Keys.FirstOrDefault() ?? "Unknown";
        }
        
        _logger?.LogInformation("Message classified as skill type: {SkillType}", skillType);
        return skillType;
    }

    /// <summary>
    /// Infers the appropriate skill based on the content of a message
    /// </summary>
    /// <param name="message">The message content to analyze</param>
    /// <returns>The inferred skill type</returns>
    public async Task<string> InferSkillFromMessage(string message)
    {
        return await ClassifySkill(message);
    }

    protected async Task<string> GenerateResponseUsing(string agentPrompt, string userName, string userAsk)
    {
        ChatMessage systemChatMessage = new ChatMessage(ChatRole.System, agentPrompt);
        systemChatMessage.AuthorName = Description;

        foreach(var knowledgeInstruction in ConversationState.KnowledgeInstructions)
        {
            systemChatMessage.Contents.Add(new TextContent(knowledgeInstruction));
        }

        ChatMessage userAskMessage = new ChatMessage(ChatRole.User, userAsk);
        userAskMessage.AuthorName = userName;

        List<ChatMessage> generationConversation = [systemChatMessage, .. ConversationState.UserAsks];

        ChatResponse chatResponse = await _chatClient.GetResponseAsync(
            generationConversation,
            _chatOptions);

        _logger?.LogDebug($"Response {chatResponse.Text}");

        ConversationState.AddGeneration(userAskMessage, chatResponse);

        return ConversationState.GetLastGeneration();
    }
}
