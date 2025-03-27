// Copyright (c) Microsoft Corporation. All rights reserved.
// Program.cs
using Aspire.Hosting.Python;
using Microsoft.Extensions.Hosting;
const string pythonHelloAgentPath = "../core_xlang_hello_python_agent";
const string pythonHelloAgentPy = "hello_python_agent.py";
const string pythonVEnv = "../../../../python/.venv";
//Environment.SetEnvironmentVariable("XLANG_TEST_NO_DOTNET", "true");
//Environment.SetEnvironmentVariable("XLANG_TEST_NO_PYTHON", "true");

var distributedApplicationBuilder = DistributedApplication.CreateBuilder(args);
var autoGenAgentHost = distributedApplicationBuilder.AddProject<Projects.Microsoft_AutoGen_AgentHost>("AgentHost").WithExternalHttpEndpoints();

IResourceBuilder<ProjectResource>? dotnetAgent = null;
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XLANG_TEST_NO_DOTNET")))
{
    dotnetAgent = distributedApplicationBuilder.AddProject<Projects.HelloAgentTests>("HelloAgentTestsDotNET")
        .WithReference(autoGenAgentHost)
        .WithEnvironment("AGENT_HOST", autoGenAgentHost.GetEndpoint("https"))
        .WithEnvironment("STAY_ALIVE_ON_GOODBYE", "true")
        .WaitFor(autoGenAgentHost);
}

#pragma warning disable ASPIREHOSTINGPYTHON001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
IResourceBuilder<PythonAppResource>? pythonAgent = null;
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XLANG_TEST_NO_PYTHON")))
{
    // xlang is over http for now - in prod use TLS between containers
    pythonAgent = distributedApplicationBuilder.AddPythonApp("HelloAgentTestsPython", pythonHelloAgentPath, pythonHelloAgentPy, pythonVEnv)
        .WithReference(autoGenAgentHost)
        .WithEnvironment("AGENT_HOST", autoGenAgentHost.GetEndpoint("http"))
        .WithEnvironment("STAY_ALIVE_ON_GOODBYE", "true")
        .WithEnvironment("GRPC_DNS_RESOLVER", "native")
        .WithOtlpExporter()
        .WaitFor(autoGenAgentHost);
#pragma warning restore ASPIREHOSTINGPYTHON001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    if (dotnetAgent != null) { pythonAgent.WaitFor(dotnetAgent); }
}

using var distributedApp = distributedApplicationBuilder.Build();
await distributedApp.StartAsync();

var url = autoGenAgentHost.GetEndpoint("http").Url;
Console.WriteLine("Backend URL: " + url);

if (dotnetAgent != null) { Console.WriteLine("Dotnet Resource Projects.HelloAgentTests invoked as HelloAgentTestsDotNET"); }
if (pythonAgent != null) { Console.WriteLine("Python Resource hello_python_agent.py invoked as HelloAgentTestsPython"); }

await distributedApp.WaitForShutdownAsync();
