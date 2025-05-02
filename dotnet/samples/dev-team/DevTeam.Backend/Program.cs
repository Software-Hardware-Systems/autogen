// Copyright (c) Microsoft Corporation. All rights reserved.
// Program.cs

//using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
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
using Microsoft.AutoGen.Extensions.SemanticKernel;
using Microsoft.AutoGen.Protobuf;
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

webAppBuilder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            | System.Security.Authentication.SslProtocols.Tls13;

        // Load the certificate from configuration
        var certPath = webAppBuilder.Configuration["DevCert:Path"];
        var certPassword = webAppBuilder.Configuration["DevCert:Password"];
        if (!string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(certPassword))
        {
            httpsOptions.ServerCertificate = new X509Certificate2(certPath, certPassword);
        }
        else
        {
            throw new InvalidOperationException("Certificate path or password is not configured.");
        }
    });

    // Ensure Kestrel listens on the required ports
    //options.ListenLocalhost(5244, listenOptions =>
    //{
    //    listenOptions.UseHttps();
    //});
});

// The using alias points to the DevTeam.ServiceDefaults implementation of AddServiceDefaults
DevTeamServiceDefaults.AddServiceDefaults(webAppBuilder);

// Azure.AI is used for chat completion services
webAppBuilder.AddChatCompletionService("AIClientOptions");

// Semantic Kernel is used for VectorMemory for knowledge documents
webAppBuilder.ConfigureSemanticKernel();

// Add gRPC services and configure the gRPC client to connect to the Agent-Host.
webAppBuilder.Services.AddGrpc();
webAppBuilder.Services.AddGrpcClient<AgentRpc.AgentRpcClient>(options =>
{
    // Ensure the AGENT_HOST configuration key is set to a valid URI.
    var agentHost = webAppBuilder.Configuration["AGENT_HOST"];
    if (string.IsNullOrEmpty(agentHost))
    {
        throw new InvalidOperationException("The AGENT_HOST configuration is missing or invalid.");
    }

    options.Address = new Uri(agentHost);
});

// Register the GrpcAgentRuntime, which manages the gRPC runtime for agents.
webAppBuilder.Services.AddSingleton<GrpcAgentRuntime>();
// Register the GrpcAgentRuntime as the implementation for IAgentRuntime.
webAppBuilder.Services.AddSingleton<IAgentRuntime, GrpcAgentRuntime>();

// Configure GitHub options and validate them on startup.
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

webAppBuilder.Services.AddTransient(s =>
{
    var ghOptions = s.GetRequiredService<IOptions<GithubOptions>>();
    var logger = s.GetRequiredService<ILogger<GithubAuthService>>();
    var ghService = new GithubAuthService(ghOptions, logger);
    var client = ghService.GetGitHubClient();
    return client;
});

// Configure Azure clients for interacting with Azure resources.
webAppBuilder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddArmClient(default);
    clientBuilder.UseCredential(new DefaultAzureCredential());
});

// Register other application services.
webAppBuilder.Services.AddSingleton<WebhookEventProcessor, GithubWebHookProcessor>();
webAppBuilder.Services.AddSingleton<IManageGithub, GithubService>();
webAppBuilder.Services.AddSingleton<IManageAzure, AzureService>();

// Build the application.
var app = webAppBuilder.Build();

// Configure the AgentsAppBuilder to register agents with the gRPC Agent-Host.
// This demonstrates how to register multiple agents for the sample application.
AgentsAppBuilder agentsAppBuilder = new AgentsAppBuilder();
agentsAppBuilder.AddGrpcAgentWorker(webAppBuilder.Configuration["AGENT_HOST"]!)
    .AddAgent<AzureGenie>(nameof(AzureGenie))
    .AddAgent<Sandbox>(nameof(Sandbox))
    .AddAgent<Hubber>(nameof(Hubber))
    .AddAgent<Dev>(nameof(Dev))
    .AddAgent<ProductManager>(nameof(ProductManager))
    .AddAgent<DeveloperLead>(nameof(DeveloperLead));

// Map default endpoints for the application.
AspireHostingExtensions.MapDefaultEndpoints(app);

// Configure routing and endpoints for GitHub webhooks and gRPC services.
app.UseRouting()
   .UseEndpoints(endpoints =>
   {
       var ghOptions = app.Services.GetRequiredService<IOptions<GithubOptions>>().Value;
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
app.Run();
