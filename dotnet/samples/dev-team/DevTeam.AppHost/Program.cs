// Copyright (c) Microsoft Corporation. All rights reserved.
// Program.cs

//using Aspire.Hosting.Python;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

var distributedAppBuilder = DistributedApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Log to console
    .WriteTo.File("logs/apphost-log-.txt", rollingInterval: RollingInterval.Day) // Log to file
    .Enrich.FromLogContext()
    .CreateLogger();

// Add user secrets
distributedAppBuilder.Configuration.AddUserSecrets<Program>();

string environment;
#if DEBUG
    environment = Environments.Development;
#else
    environment = Environments.Production;
#endif

// We use Grpc Hosting infrastructure so we can involve agents defined in Python land
IResourceBuilder<ContainerResource> autoGenAgentHostContainer;
EndpointReference agentHostHttpsEndpointForDotnetBackend;
EndpointReference agentHostHttpEndpointForXlang; // Python agent host endpoint

// if we are using an external Grpc host, then environment variable AGENT_HOST will already be defined
if (Environment.GetEnvironmentVariable("AGENT_HOST") != null)
{
    // Figure out how to reference and utilize the Grpc host using AGENT_HOST
    throw new Exception("External Grpc host not yet supported");
}
else
{
    int agentHostHttpsPort = 5001;
    int agentHostHttpPort = 5000;

    // The AutoGen agent host, managing agent message pub/sub, runs in docker
    autoGenAgentHostContainer = distributedAppBuilder
        .AddContainer(name: "agent-host", image: "autogen-host")
        .WithEnvironment("ASPNETCORE_URLS", $"https://+:{agentHostHttpsPort.ToString()};http://+:{agentHostHttpPort.ToString()}")
        .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", distributedAppBuilder.Configuration["AgentHostCert:Password"])
        .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/https/AgentHostCert.pfx")
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", environment)
        .WithBindMount(Path.GetDirectoryName(distributedAppBuilder.Configuration["AgentHostCert:Path"]) ?? "./certs", "/https", true)
        .WithHttpsEndpoint(port: agentHostHttpsPort, targetPort: agentHostHttpsPort, name: "agent-host-https-endpoint")
        .WithHttpEndpoint(port: agentHostHttpPort, targetPort: agentHostHttpPort, name: "agent-host-http-endpoint")
        ?? throw new Exception("Failed to create autoGenAgentHost");

    // The https endpoint will be used for the dotnet backend agents
    agentHostHttpsEndpointForDotnetBackend = autoGenAgentHostContainer.GetEndpoint("agent-host-https-endpoint");
    // The http endpoint will be used for the xlang (python) agents
    agentHostHttpEndpointForXlang = autoGenAgentHostContainer.GetEndpoint("agent-host-http-endpoint");
}

// Vector Database used for knowledge stores
var qdrant = distributedAppBuilder.AddQdrant("qdrant");

// Add DevTeam Backend project defines the DevTeam AutoGen agents
//var dotnetDevTeam =
var dotnetDevTeam = distributedAppBuilder.AddProject<Projects.DevTeam_Backend>("backend")
    // Pass in the agent host endpoint and initialize the AGENT_HOST environment variable
    //.WithReference(agentHostHttpsEndpoint)
    .WithEnvironment("AGENT_HOST", $"{agentHostHttpsEndpointForDotnetBackend.Property(EndpointProperty.Url)}")
    .WithEnvironment("AgentHostCert__Path", distributedAppBuilder.Configuration["AgentHostCert:Path"])
    .WithEnvironment("AgentHostCert__Password", distributedAppBuilder.Configuration["AgentHostCert:Password"])
    // The DevTeam Backend project uses the Qdrant vector database for knowledge stores
    .WithEnvironment("Qdrant__Endpoint", $"{qdrant.Resource.HttpEndpoint.Property(EndpointProperty.Url)}")
    .WithEnvironment("Qdrant__ApiKey", $"{qdrant.Resource.ApiKeyParameter.Value}")
    .WithEnvironment("Qdrant__VectorSize", distributedAppBuilder.Configuration["Qdrant:VectorSize"])
    .WithEnvironment("OpenAI__Key", distributedAppBuilder.Configuration["OpenAI:Key"])
    .WithEnvironment("OpenAI__Endpoint", distributedAppBuilder.Configuration["OpenAI:Endpoint"])
    .WithReference(qdrant)
    // The DevTeam Backend project uses the Github Webhook
    .WithEnvironment("Github__AppId", distributedAppBuilder.Configuration["Github:AppId"])
    .WithEnvironment("Github__InstallationId", distributedAppBuilder.Configuration["Github:InstallationId"])
    .WithEnvironment("Github__WebhookSecret", distributedAppBuilder.Configuration["Github:WebhookSecret"])
    .WithEnvironment("Github__AppKey", distributedAppBuilder.Configuration["Github:AppKey"])
    .WithHttpsEndpoint(port: 5244, targetPort: 5244, isProxied: false, name: "githubapp-https-webhook")
    .WaitFor(autoGenAgentHostContainer)
    .WaitFor(qdrant);

//TODO: add this to the config in backend
//.WithEnvironment("", acaSessionsEndpoint);

// Use this as a starting point to somehow get MagenticOne into the mix of AutoGen agents
//const string pythonHelloAgentPath = "../core_xlang_hello_python_agent";
//const string pythonHelloAgentPy = "hello_python_agent.py";
//const string pythonVEnv = "../../../../python/.venv";
//#pragma warning disable ASPIREHOSTINGPYTHON001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
//IResourceBuilder<PythonAppResource>? python;
//// xlang is over http for now - in prod use TLS between containers
//python = distributedAppBuilder.AddPythonApp("HelloAgentTestsPython", pythonHelloAgentPath, pythonHelloAgentPy, pythonVEnv)
//    .WithReference(autoGenAgentHostContainer)
//    .WithEnvironment("AGENT_HOST", $"{agentHostHttpEndpointForXlang.Property(EndpointProperty.Url)}")
//    .WithEnvironment("STAY_ALIVE_ON_GOODBYE", "true")
//    .WithEnvironment("GRPC_DNS_RESOLVER", "native")
//    .WithOtlpExporter()
//    .WaitFor(autoGenAgentHost);
//if (dotnetDevTeam != null) { python.WaitFor(dotnetDevTeam); }
//#pragma warning restore ASPIREHOSTINGPYTHON001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using var app = distributedAppBuilder.Build();
await app.StartAsync();
await app.WaitForShutdownAsync();
