// Copyright (c) Microsoft Corporation. All rights reserved.
// Program.cs

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using DevTeam.Backend;
using DevTeam.Backend.Agents;
using DevTeam.Backend.Agents.Developer;
using DevTeam.Backend.Agents.DeveloperLead;
using DevTeam.Backend.Agents.ProductManager;
using DevTeam.Backend.Agents.Stakeholder;
using DevTeam.Backend.Services;
using DevTeam.Options;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Core;
using Microsoft.AutoGen.Core.Grpc;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;
using Serilog;

using DevTeamServiceDefaults = Microsoft.Extensions.Hosting.Extensions;

// Configure the web application builder.
var webAppBuilder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Log to console
    .WriteTo.File("logs/backend-log-.txt", rollingInterval: RollingInterval.Day) // Log to file
    .Enrich.FromLogContext()
    .CreateLogger();
webAppBuilder.Host.UseSerilog(); // Use Serilog as the logging provider// Configure logging to use console, debug, and configuration-based settings.

webAppBuilder.Logging.ClearProviders();
webAppBuilder.Logging.AddSerilog();

// Add user secrets for secure storage of sensitive information.
webAppBuilder.Configuration.AddUserSecrets<Program>();

X509Certificate2? webAppServerCertificate = null;
var kestrel = webAppBuilder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            | System.Security.Authentication.SslProtocols.Tls13;

        // Load the certificate from configuration
        var webAppServerCertPath = webAppBuilder.Configuration["WebAppServerCert:Path"];
        var webAppServerCertPassword = webAppBuilder.Configuration["WebAppServerCert:Password"];
        if (!string.IsNullOrEmpty(webAppServerCertPath) && !string.IsNullOrEmpty(webAppServerCertPassword))
        {
            webAppServerCertificate = new X509Certificate2(webAppServerCertPath, webAppServerCertPassword);
            httpsOptions.ServerCertificate = webAppServerCertificate;
        }
        else
        {
            throw new InvalidOperationException("WebApp server certificate path or password is not configured.");
        }

        // Require client certificates
        httpsOptions.ClientCertificateMode = Debugger.IsAttached ? ClientCertificateMode.NoCertificate : ClientCertificateMode.RequireCertificate;
        httpsOptions.CheckCertificateRevocation = Debugger.IsAttached ? false : true;
    });
});

// The using alias points to the DevTeam.ServiceDefaults implementation of AddServiceDefaults
DevTeamServiceDefaults.AddServiceDefaults(webAppBuilder);

// gRPC AspNetCore support is used for AutoGen agent communication infrastructure
webAppBuilder.Services.AddGrpc();

webAppBuilder.Services.AddOptions<GithubOptions>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("GithubOptions").Bind(settings);
    })
    .ValidateDataAnnotations()
    .ValidateOnStart()
    .PostConfigure(options =>
    {
        if (string.IsNullOrEmpty(options.WebhookSecret))
        {
            Console.WriteLine("Warning: GitHub WebhookSecret is not configured.");
        }
    });

// The WebhookEventProcessor listens for GitHub webhook events and publishes messages to DevTeam agents.
webAppBuilder.Services.AddSingleton<WebhookEventProcessor, GithubWebHookProcessor>();

// Now create the AgentsAppBuilder and register the agents that will be responding
// to the gRPC messages published by the GitHubWebHookProcessor
AgentsAppBuilder agentsAppBuilder = new AgentsAppBuilder();

// Configure the AgentsAppBuilder to register agents with the gRPC Agent-Host.
agentsAppBuilder.Configuration["AGENT_HOST"] = webAppBuilder.Configuration["AGENT_HOST"];
agentsAppBuilder.Configuration["AgentHostCert:Path"] = webAppBuilder.Configuration["AgentHostCert:Path"];
agentsAppBuilder.Configuration["AgentHostCert:Password"] = webAppBuilder.Configuration["AgentHostCert:Password"];

// Propagate GithubOptions configuration values to AgentsAppBuilder
agentsAppBuilder.Configuration["GithubOptions:AppId"] = webAppBuilder.Configuration["GithubOptions:AppId"];
agentsAppBuilder.Configuration["GithubOptions:InstallationId"] = webAppBuilder.Configuration["GithubOptions:InstallationId"];
agentsAppBuilder.Configuration["GithubOptions:ClientId"] = webAppBuilder.Configuration["GithubOptions:ClientId"];
agentsAppBuilder.Configuration["GithubOptions:AppKey"] = webAppBuilder.Configuration["GithubOptions:AppKey"];
agentsAppBuilder.Configuration["GithubOptions:WebhookSecret"] = webAppBuilder.Configuration["GithubOptions:WebhookSecret"];

// Propagate AzureOptions configuration values to AgentsAppBuilder
agentsAppBuilder.Configuration["AzureOptions:SubscriptionId"] = webAppBuilder.Configuration["AzureOptions:SubscriptionId"];
agentsAppBuilder.Configuration["AzureOptions:Location"] = webAppBuilder.Configuration["AzureOptions:Location"];
agentsAppBuilder.Configuration["AzureOptions:ContainerInstancesResourceGroup"] = webAppBuilder.Configuration["AzureOptions:ContainerInstancesResourceGroup"];
agentsAppBuilder.Configuration["AzureOptions:FilesShareName"] = webAppBuilder.Configuration["AzureOptions:FilesShareName"];
agentsAppBuilder.Configuration["AzureOptions:FilesAccountName"] = webAppBuilder.Configuration["AzureOptions:FilesAccountName"];
agentsAppBuilder.Configuration["AzureOptions:FilesAccountKey"] = webAppBuilder.Configuration["AzureOptions:FilesAccountKey"];
agentsAppBuilder.Configuration["AzureOptions:SandboxImage"] = webAppBuilder.Configuration["AzureOptions:SandboxImage"];

agentsAppBuilder.AddGrpcAgentWorker()
    .AddAgent<Stakeholder>(nameof(Stakeholder))
    .AddAgent<ProductManager>(nameof(ProductManager))
    .AddAgent<DeveloperLead>(nameof(DeveloperLead))
    .AddAgent<Dev>(nameof(Dev))
    .AddAgent<AzureGenie>(nameof(AzureGenie))
    .AddAgent<Sandbox>(nameof(Sandbox))
    .AddAgent<Hubber>(nameof(Hubber))
    ;

agentsAppBuilder.Services.AddMemoryCache(); // Registers IMemoryCache
agentsAppBuilder.Services.AddDistributedMemoryCache(); // Registers IDistributedCache as MemoryDistributedCache

// These extension methods are used to register the AI clients for chat and embedding generation services
// for a WebAppBuilder but don't work with AgentAppBuilder
// webAppBuilder.AddChatCompletionService("AIClientOptions");
// webAppBuilder.AddEmbeddingGeneratorService("AIClientOptions");
// So we register them here instead on the ServiceCollection but we lose the configuration options and validation
// ToDo: Fix this to use the configuration options and validation
agentsAppBuilder.Services.AddOllamaChatClient(webAppBuilder.Configuration["AIClientOptions:LlmModelName"] ?? string.Empty);
agentsAppBuilder.Services.AddOllamaEmbeddingGenerator(webAppBuilder.Configuration["AIClientOptions:LlmModelName"] ?? string.Empty);

// ToDo: Similar problem here with the Semantic Kernel as above with the Chat and Embedding services
// Semantic Kernel is used for storing knowledge documents into VectorMemory
//webAppBuilder.ConfigureSemanticKernel();

// Configure GitHub options and validate them on startup.
agentsAppBuilder.Services.AddOptions<GithubOptions>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("GithubOptions").Bind(settings);
    })
    .ValidateDataAnnotations()
    .ValidateOnStart()
    .PostConfigure(options =>
    {
        if (string.IsNullOrEmpty(options.WebhookSecret))
        {
            Console.WriteLine("Warning: GitHub WebhookSecret is not configured.");
        }
    });

// The Github client is transient and used for interacting with GitHub APIs.
agentsAppBuilder.Services.AddTransient(s =>
{
    var ghOptions = s.GetRequiredService<IOptions<GithubOptions>>();
    var logger = s.GetRequiredService<ILogger<GithubAuthService>>();
    var ghService = new GithubAuthService(ghOptions, logger);
    var githubClient = ghService.GetGitHubClient();
    return githubClient;
});
// The GithubService is a singleton and used by the agents to perform operations like creating issues, branches, and pull requests.
agentsAppBuilder.Services.AddSingleton<IManageGithub, GithubService>();

// Configure Azure clients for interacting with Azure resources.
agentsAppBuilder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddArmClient(default);
    clientBuilder.UseCredential(new DefaultAzureCredential());
});
// The AzureService is a singleton and used by the agents to perform operations like storing code or documents in Azure Blob Storage and running code in a sandbox environment.
agentsAppBuilder.Services.AddSingleton<IManageAzure, AzureService>();

var agentsApp = await agentsAppBuilder.BuildAsync();
await agentsApp.StartAsync();

// Register the AgentsApp as a singleton service in the web application builder.
// This allows the web application (GithubWebhookProcessor) to access the agents and their services.
var agentRuntime = agentsApp.Services.GetRequiredService<IAgentRuntime>();
webAppBuilder.Services.AddSingleton(agentRuntime);

// Build the application.
var webApp = webAppBuilder.Build();

// Configure routing and endpoints for GitHub webhooks and gRPC services.
webApp.UseRouting()
   .UseEndpoints(endpoints =>
   {
       var ghOptions = webApp.Services.GetRequiredService<IOptions<GithubOptions>>().Value;
       endpoints.MapGitHubWebhooks(secret: ghOptions.WebhookSecret);

       // Map the gRPC service to handle gRPC requests.
       endpoints.MapGrpcService<GrpcAgentService>();
   });

// Enable Swagger for API documentation.
//app.UseSwagger();
//app.UseSwaggerUI(c =>
//{
//    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DevTeam API V1");
//});

// Run the application.
webApp.Run();
