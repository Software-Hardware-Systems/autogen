// Copyright (c) Microsoft Corporation. All rights reserved.
// AiAgent.cs

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AutoGen.AgentChat.State;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Agents;

public class AiAgent<T> : BaseAgent
{
    public AiAgent(
        ISemanticTextMemory semanticTextMemory,
        IChatClient chatClient,
        AgentId id,
        IAgentRuntime runtime,
        ILogger<AiAgent<T>>? logger = null)
        :
        base(id, runtime, nameof(AiAgent<T>), logger)
    {
        _semanticTextMemory = semanticTextMemory;
        _chatClient = chatClient;
        _chatOptions = new() { Tools = [AIFunctionFactory.Create(RetrieveAdditionalKnowledge)] };
    }

    private ISemanticTextMemory _semanticTextMemory;
    private IChatClient _chatClient;
    private ChatOptions _chatOptions;

    protected AiAgentConversationState ConversationState { get; } = new();

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
        IAsyncEnumerable<MemoryQueryResult> retrievedKnowledgeResults = _semanticTextMemory.SearchAsync(knowledgeCollection, input, limit);

        var retrievedKnowledgePromptBuilder = new StringBuilder();
        await foreach (var retrievedKnowledgeItem in retrievedKnowledgeResults)
        {
            retrievedKnowledgePromptBuilder.AppendLine(retrievedKnowledgeItem.Metadata.Text);
        }
        var retrievedKnowledge = retrievedKnowledgePromptBuilder.ToString();

        return retrievedKnowledge;
    }

    protected async Task<string> GenerateResponseUsing(string agentPrompt, string UserName, string userAsk)
    {
        ChatMessage systemChatMessage = new ChatMessage(ChatRole.System, agentPrompt);
        systemChatMessage.AuthorName = Description;

        foreach(var knowledgeInstruction in ConversationState.KnowledgeInstructions)
        {
            systemChatMessage.Contents.Add(new TextContent(knowledgeInstruction));
        }

        ChatMessage userAskMessage = new ChatMessage(ChatRole.User, userAsk);
        userAskMessage.AuthorName = UserName;

        List<ChatMessage> generationConversation = [systemChatMessage, .. ConversationState.UserAsks];

        ChatResponse chatResponse = await _chatClient.GetResponseAsync(
            generationConversation,
            _chatOptions);

        ConversationState.AddGeneration(userAskMessage, chatResponse);

        return ConversationState.GetLastGeneration();
    }
}
