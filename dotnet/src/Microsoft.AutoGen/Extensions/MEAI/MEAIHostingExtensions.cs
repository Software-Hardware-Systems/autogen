// Copyright (c) Microsoft Corporation. All rights reserved.
// MEAIHostingExtensions.cs

using Microsoft.Extensions.AI;

namespace Microsoft.Extensions.Hosting;

public static class MEAIHostingExtensions
{
    public static IHostApplicationBuilder AddChatCompletionService(this IHostApplicationBuilder builder, string serviceName)
    {
        var pipeline = (ChatClientBuilder pipeline) => pipeline
        .UseLogging()
        .UseFunctionInvocation()
        .UseOpenTelemetry(configure: c => c.EnableSensitiveData = true);

        if (builder.Configuration[$"{serviceName}:ModelType"] == "ollama")
        {
            builder.AddOllamaChatClient(serviceName, pipeline);
        }
        else if (builder.Configuration[$"{serviceName}:ModelType"] == "openai" || builder.Configuration[$"{serviceName}:ModelType"] == "azureopenai")
        {
            builder.AddOpenAIChatClient(serviceName, pipeline);
        }
        else if (builder.Configuration[$"{serviceName}:ModelType"] == "azureaiinference")
        {
            builder.AddAzureChatClient(serviceName, pipeline);
        }
        else
        {
            throw new InvalidOperationException("Did not find a valid model implementation for the given service name ${serviceName}, valid supported implemenation types are ollama, openai, azureopenai, azureaiinference");
        }
        return builder;
    }

    public static IHostApplicationBuilder AddEmbeddingGeneratorService(this IHostApplicationBuilder builder, string serviceName)
    {
        var pipeline = (EmbeddingGeneratorBuilder<string, Embedding<float>> pipeline) => pipeline
        .UseLogging()
        .UseOpenTelemetry();

        if (builder.Configuration[$"{serviceName}:ModelType"] == "ollama")
        {
            builder.AddOllamaEmbeddingGenerator(serviceName, pipeline);
        }
        else if (builder.Configuration[$"{serviceName}:ModelType"] == "openai" || builder.Configuration[$"{serviceName}:ModelType"] == "azureopenai")
        {
            throw new NotImplementedException("Embedding generation is not yet supported for OpenAI or Azure OpenAI. Please use Ollama instead.");
        }
        else if (builder.Configuration[$"{serviceName}:ModelType"] == "azureaiinference")
        {
            throw new NotImplementedException("Embedding generation is not yet supported for OpenAI or Azure OpenAI. Please use Ollama instead.");
        }
        else
        {
            throw new InvalidOperationException($"Did not find a valid embedding model implementation for the given service name {serviceName}");
        }
        return builder;
    }
}
