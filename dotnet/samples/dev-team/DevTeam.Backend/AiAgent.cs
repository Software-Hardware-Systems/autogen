// Copyright (c) Microsoft Corporation. All rights reserved.
// AiAgent.cs

using AutoGen.Core;
using Microsoft.AutoGen.Agents;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.SemanticKernel.Memory;

namespace DevTeam.Agents;

public class AiAgent<T>(
        ISemanticTextMemory semanticTextMemory,
        AutoGen.Core.IAgent coreAgentUsedForInference,
        IHostApplicationLifetime hostApplicationLifetime,
        AgentId id,
        IAgentRuntime runtime,
        ILogger<AiAgent<T>>? logger = null)
        :
        BaseAgent(id, runtime, nameof(AiAgent<T>), logger),
        IHandle<NewMessageReceived>,
        IHandle<ConversationClosed>,
        IHandle<Shutdown>
{
    private IMessage? instructionMessage;
    private IMessage? userAskMessage;

    public async ValueTask HandleAsync(NewMessageReceived item, MessageContext messageContext)
    {
        Console.Out.WriteLine(item.Message); // Print message to console
        await ValueTask.CompletedTask;
    }

    public async ValueTask HandleAsync(ConversationClosed item, MessageContext messageContext)
    {
        var goodbye = $"{item.UserId} said {item.UserMessage}"; // Print goodbye message to console
        Console.Out.WriteLine(goodbye);
        if (Environment.GetEnvironmentVariable("STAY_ALIVE_ON_GOODBYE") != "true")
        {
            // Publish message that will be handled by shutdown handler
            await this.PublishMessageAsync(new Shutdown(), new TopicId("HelloTopic"));
        }
    }

    public async ValueTask HandleAsync(Shutdown item, MessageContext messageContext)
    {
        Console.WriteLine("Shutting down...");
        hostApplicationLifetime.StopApplication(); // Shuts down application
    }

    protected async Task AddKnowledge(string instruction, string ask)
    {
        // Search the vector store for relevant information
        var searchResults = semanticTextMemory.SearchAsync("waf", ask, limit: 5);

        // Extract the relevant information from the search results
        var knowledge = string.Join("\n", searchResults.Select(result => result.Metadata.Text));

        // Create and add messages to chat history
        userAskMessage = new AutoGen.Core.TextMessage(Role.User, ask, "user");

        instructionMessage = new AutoGen.Core.TextMessage(Role.System, $"{instruction}\n{knowledge}", "system");
    }

    protected async Task<string> CallFunction(string prompt)
    {
        IMessage message = new AutoGen.Core.TextMessage(
            Role.Assistant,
            prompt,
            "assistant");

        List<IMessage> promptMessages = new List<IMessage> { message };
        if (instructionMessage != null) { promptMessages.Add(instructionMessage); }
        if (userAskMessage != null) { promptMessages.Add(userAskMessage); }

        // Use the AutoGen.Core.IAgent to obtain an LLM inference response
        var responseMessage = await coreAgentUsedForInference.GenerateReplyAsync(promptMessages);

        // Extract the content from the responseMessage and return it as a string
        if (responseMessage is IMessage<string> textResponse)
        {
            return textResponse.Content;
        }
        else if (responseMessage is AutoGen.Core.TextMessage textMessage)
        {
            return textMessage.Content;
        }
        else
        {
            throw new InvalidOperationException("Unexpected response message type.");
        }
    }
}
