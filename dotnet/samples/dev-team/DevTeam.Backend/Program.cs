// Copyright (c) Microsoft Corporation. All rights reserved.
// Program.cs

using Azure.AI.Inference;
using Azure.Identity;
using DevTeam.Agents;
using DevTeam.Backend;
using DevTeam.Backend.Agents;
using DevTeam.Backend.Agents.Developer;
using DevTeam.Backend.Agents.DeveloperLead;
using DevTeam.Backend.Agents.ProductManager;
using DevTeam.Backend.Services;
using DevTeam.Options;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.Core.Grpc;
using Microsoft.AutoGen.Protobuf;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Memory;
using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;

var webAppBuilder = WebApplication.CreateBuilder(args);

// Add user secrets
webAppBuilder.Configuration.AddUserSecrets<Program>();

// Add logging configuration
webAppBuilder.Logging.ClearProviders();
webAppBuilder.Logging.AddConsole();
webAppBuilder.Logging.AddDebug();
webAppBuilder.Logging.AddConfiguration(webAppBuilder.Configuration.GetSection("Logging"));

// Service Defaults for Aspire, SemanticKernel, and ChatCompletionsClient
webAppBuilder.AddServiceDefaults();
webAppBuilder.Services.AddHttpClient();
webAppBuilder.Services.AddControllers();
webAppBuilder.Services.AddSwaggerGen();

// Grpc
webAppBuilder.Services.AddGrpc();
webAppBuilder.Services.AddGrpcClient<AgentRpc.AgentRpcClient>(options =>
{
    options.Address = new Uri(webAppBuilder.Configuration["AGENT_HOST"]!);
});
webAppBuilder.Services.AddSingleton<GrpcAgentRuntime>();

// GitHub
webAppBuilder.Services.AddOptions<GithubOptions>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("GitHub").Bind(settings);
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();
webAppBuilder.Services.AddSingleton<GithubAuthService>();
webAppBuilder.Services.AddTransient(s =>
{
    var ghOptions = s.GetRequiredService<IOptions<GithubOptions>>();
    var logger = s.GetRequiredService<ILogger<GithubAuthService>>();
    var ghService = new GithubAuthService(ghOptions, logger);
    var client = ghService.GetGitHubClient();
    return client;
});
webAppBuilder.Services.AddSingleton<WebhookEventProcessor, GithubWebHookProcessor>();
webAppBuilder.Services.AddSingleton<IManageGithub, GithubService>();

// Azure
webAppBuilder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddArmClient(default);
    clientBuilder.UseCredential(new DefaultAzureCredential());
});
webAppBuilder.Services.AddSingleton<IManageAzure, AzureService>();

// AutoGen Core IAgent used to perform LLM Inference
webAppBuilder.Services.AddSingleton<AutoGen.Core.IAgent, AutoGen.AzureAIInference.ChatCompletionsClientAgent>(sp =>
{
    // The Inference Agent created should be a function of the AutoGen Agent purpose
    // Dev, ProductManager, DeveloperLead, etc.
    // Consider a factory pattern to create the appropriate Inference Agent
    // That way any of the ChatCompletionsClient implementations can be injected into AiAgent and used for inference
    // The factory should look for available configuration values and use them to determine which implementation to create
    // Right now we're hard coded for Azure AI Inference
    var chatCompletionsClient = sp.GetRequiredService<ChatCompletionsClient>();
    return new AutoGen.AzureAIInference.ChatCompletionsClientAgent(chatCompletionsClient, "CoreInferenceAgent", "gpt-4o-mini");
});

// Register the Hubber agent and its dependencies
webAppBuilder.Services.AddSingleton<Hubber>(sp =>
{
    var ghService = sp.GetRequiredService<IManageGithub>();
    var semanticTextMemory = sp.GetRequiredService<ISemanticTextMemory>();
    var coreAgent = sp.GetRequiredService<AutoGen.Core.IAgent>();
    var hostApplicationLifetime = sp.GetRequiredService<IHostApplicationLifetime>();
    var id = new Microsoft.AutoGen.Contracts.AgentId("Hubber", "default");
    var runtime = sp.GetRequiredService<IAgentRuntime>();
    var logger = sp.GetRequiredService<ILogger<AiAgent<Hubber>>>();
    return new Hubber(ghService, semanticTextMemory, coreAgent, hostApplicationLifetime, id, runtime, logger);
});

// Register AiAgent<Hubber>
webAppBuilder.Services.AddSingleton<AiAgent<Hubber>>(sp => sp.GetRequiredService<Hubber>());

var app = webAppBuilder.Build();

AgentsAppBuilder agentsAppBuilder = new AgentsAppBuilder();
agentsAppBuilder.AddGrpcAgentWorker(webAppBuilder.Configuration["AGENT_HOST"]!)
    .AddAgent<AzureGenie>(nameof(AzureGenie))
    .AddAgent<Sandbox>(nameof(Sandbox))
    .AddAgent<Hubber>(nameof(Hubber))
    .AddAgent<Dev>(nameof(Dev))
    .AddAgent<ProductManager>(nameof(ProductManager))
    .AddAgent<DeveloperLead>(nameof(DeveloperLead));

app.MapDefaultEndpoints();
app.UseRouting()
.UseEndpoints(endpoints =>
{
    var ghOptions = app.Services.GetRequiredService<IOptions<GithubOptions>>().Value;
    endpoints.MapGitHubWebhooks(secret: ghOptions.WebhookSecret);
    endpoints.MapGrpcService<GrpcAgentService>();
});

app.UseSwagger();
/* app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
}); */

app.Run();
