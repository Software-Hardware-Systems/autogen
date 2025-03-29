// Copyright (c) Microsoft Corporation. All rights reserved.
// Program.cs

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var distributedAppBuilder = DistributedApplication.CreateBuilder(args);

// Add user secrets
distributedAppBuilder.Configuration.AddUserSecrets<Program>();

// Azure used for storing and running code
distributedAppBuilder.AddAzureProvisioning();

// Vector Database used for knowledge stores
var qdrant = distributedAppBuilder.AddQdrant("qdrant");

// Grpc Hosting infrastructure
// We use Grpc so we can involve agents defined in Python land
// if we are using an external Grpc host, then environment variable AGENT_HOST will be defined
IResourceBuilder<ContainerResource> autoGenAgentHost;
EndpointReference agentHostHttps;
if (Environment.GetEnvironmentVariable("AGENT_HOST") != null)
{
    // Figure out how to reference and utilize the Grpc host using AGENT_HOST
    throw new Exception("External Grpc host not yet supported");
}
else
{
    string environment;
#if DEBUG
    environment = Environments.Development;
#else
    environment = Environments.Production;
#endif
    autoGenAgentHost = distributedAppBuilder.AddContainer("agent-host", "autogen-host")
                           .WithEnvironment("ASPNETCORE_URLS", "https://+;http://+")
                           .WithEnvironment("ASPNETCORE_HTTPS_PORTS", "5001")
                           .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Password", distributedAppBuilder.Configuration["DevCert:Password"])
                           .WithEnvironment("ASPNETCORE_Kestrel__Certificates__Default__Path", "/https/devcert.pfx")
                           .WithEnvironment("ASPNETCORE_ENVIRONMENT", environment)
                           .WithBindMount(distributedAppBuilder.Configuration["DevCert:Path"] ?? "./certs", "/https", true)
                           .WithHttpsEndpoint(targetPort: 5001) ?? throw new Exception("Failed to create autoGenAgentHost");
    agentHostHttps = autoGenAgentHost.GetEndpoint("https");
}

// Add DevTeam Backend project
//var dotnetDevTeam =
distributedAppBuilder.AddProject<Projects.DevTeam_Backend>("backend")
    .WithEnvironment("AGENT_HOST", agentHostHttps)
    .WithEnvironment("Qdrant__Endpoint", $"{qdrant.Resource.HttpEndpoint.Property(EndpointProperty.Url)}")
    .WithEnvironment("Qdrant__ApiKey", $"{qdrant.Resource.ApiKeyParameter.Value}")
    .WithEnvironment("Qdrant__VectorSize", distributedAppBuilder.Configuration["Qdrant:VectorSize"])
    .WithEnvironment("OpenAI__Key", distributedAppBuilder.Configuration["OpenAI:Key"])
    .WithEnvironment("OpenAI__Endpoint", distributedAppBuilder.Configuration["OpenAI:Endpoint"])
    .WithEnvironment("Github__AppId", distributedAppBuilder.Configuration["Github:AppId"])
    .WithEnvironment("Github__InstallationId", distributedAppBuilder.Configuration["Github:InstallationId"])
    .WithEnvironment("Github__WebhookSecret", distributedAppBuilder.Configuration["Github:WebhookSecret"])
    .WithEnvironment("Github__AppKey", distributedAppBuilder.Configuration["Github:AppKey"])
    .WaitFor(autoGenAgentHost)
    .WaitFor(qdrant);
//TODO: add this to the config in backend
//.WithEnvironment("", acaSessionsEndpoint);

//// Use this as a starting point to somehow get MagenticOne into the mix of AutoGen agents
//using Aspire.Hosting.Python;
//const string pythonHelloAgentPath = "../core_xlang_hello_python_agent";
//const string pythonHelloAgentPy = "hello_python_agent.py";
//const string pythonVEnv = "../../../../python/.venv";
//#pragma warning disable ASPIREHOSTINGPYTHON001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
//IResourceBuilder<PythonAppResource>? python;
//// xlang is over http for now - in prod use TLS between containers
//python = distributedAppBuilder.AddPythonApp("HelloAgentTestsPython", pythonHelloAgentPath, pythonHelloAgentPy, pythonVEnv)
//    .WithReference(autoGenAgentHost)
//    .WithEnvironment("AGENT_HOST", autoGenAgentHost.GetEndpoint("http"))
//    .WithEnvironment("STAY_ALIVE_ON_GOODBYE", "true")
//    .WithEnvironment("GRPC_DNS_RESOLVER", "native")
//    .WithOtlpExporter()
//    .WaitFor(autoGenAgentHost);
//if (dotnetDevTeam != null) { python.WaitFor(dotnetDevTeam); }
//#pragma warning restore ASPIREHOSTINGPYTHON001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using var app = distributedAppBuilder.Build();
await app.StartAsync();
await app.WaitForShutdownAsync();
