// Copyright (c) Microsoft Corporation. All rights reserved.
// AgentsAppBuilderExtensions.cs

using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.AutoGen.Contracts;
using Microsoft.AutoGen.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Microsoft.AutoGen.Core.Grpc;

public static class AgentsAppBuilderExtensions
{
    private const string _defaultAgentServiceAddress = "http://localhost:53071";

    public static AgentsAppBuilder AddGrpcAgentWorker(
        this AgentsAppBuilder builder,
        string? agentServiceAddress = null,
        bool useStrictDeserialization = false)
    {
        builder.Services.AddGrpcClient<AgentRpc.AgentRpcClient>(options =>
        {
            var serviceAddress = agentServiceAddress ?? builder.Configuration.GetValue("AGENT_HOST", _defaultAgentServiceAddress);
            options.Address = new Uri(serviceAddress);

            X509Certificate2 agentHostServerCert;
            var agentHostServerCertPath = builder.Configuration["AgentHostCert:Path"];
            var agentHostServerCertPassword = builder.Configuration["AgentHostCert:Password"];
            if (!string.IsNullOrEmpty(agentHostServerCertPath) && !string.IsNullOrEmpty(agentHostServerCertPassword))
            {
                agentHostServerCert = new X509Certificate2(agentHostServerCertPath, agentHostServerCertPassword);
            }
            else
            {
                throw new InvalidOperationException("WebApp server certificate path or password is not configured.");
            }

            options.ChannelOptionsActions.Add(channelOptions =>
            {
                var loggerFactory = new LoggerFactory();

                SocketsHttpHandler socketsHttpHandler;
                if (serviceAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    socketsHttpHandler = new SocketsHttpHandler
                    {
                        SslOptions = new SslClientAuthenticationOptions
                        {
                            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                            CertificateRevocationCheckMode = Debugger.IsAttached ? X509RevocationMode.NoCheck : X509RevocationMode.Online,
                            RemoteCertificateValidationCallback = (sender, remoteCertificate, chain, sslPolicyErrors) =>
                            {
                                // Log any SSL policy errors
                                if (sslPolicyErrors != SslPolicyErrors.None)
                                {
                                    Console.WriteLine($"SSL Policy Errors: {sslPolicyErrors}");
                                    return false;
                                }

                                // Ensure the chain is not null
                                if (chain == null)
                                {
                                    Console.WriteLine("Certificate chain is null.");
                                    return false;
                                }

                                // Check the chain status for critical errors
                                foreach (var chainStatus in chain.ChainStatus)
                                {
                                    if (chainStatus.Status != X509ChainStatusFlags.NoError)
                                    {
                                        Console.WriteLine($"Chain Status Error: {chainStatus.StatusInformation}");
                                        return false;
                                    }
                                }

                                // Optionally validate the root certificate against a known thumbprint
                                var rootCertificate = chain.ChainElements[^1].Certificate; // Last element is the root certificate
                                if (rootCertificate.Thumbprint != agentHostServerCert.Thumbprint)
                                {
                                    Console.WriteLine("Root certificate does not match the expected agent host certificate.");
                                    return false;
                                }

                                return true; // Certificate is valid
                            },
                        },
                    };
                }
                else
                {
                    socketsHttpHandler = new SocketsHttpHandler { };
                }
                socketsHttpHandler.EnableMultipleHttp2Connections = Debugger.IsAttached ? false : true;
                socketsHttpHandler.KeepAlivePingDelay = Debugger.IsAttached ? TimeSpan.FromSeconds(200) : TimeSpan.FromSeconds(20);
                socketsHttpHandler.KeepAlivePingTimeout = Debugger.IsAttached ? TimeSpan.FromSeconds(100) : TimeSpan.FromSeconds(10);
                socketsHttpHandler.KeepAlivePingPolicy = Debugger.IsAttached ? HttpKeepAlivePingPolicy.Always : HttpKeepAlivePingPolicy.WithActiveRequests;

                channelOptions.HttpHandler = socketsHttpHandler;

                var methodConfig = new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 5,
                        InitialBackoff = TimeSpan.FromSeconds(1),
                        MaxBackoff = TimeSpan.FromSeconds(5),
                        BackoffMultiplier = 1.5,
                        RetryableStatusCodes = { StatusCode.Unavailable }
                    }
                };

                channelOptions.ServiceConfig = new() { MethodConfigs = { methodConfig } };
                channelOptions.ThrowOperationCanceledOnCancellation = true;
            });
        });

        builder.Services.TryAddSingleton(DistributedContextPropagator.Current);
        var existingAgentRuntimeRegistration = builder.Services.FirstOrDefault(service => service.ServiceType == typeof(IAgentRuntime));
        if (existingAgentRuntimeRegistration != null)
        {
            var existingAgentRuntimeType = existingAgentRuntimeRegistration.ImplementationType ?? existingAgentRuntimeRegistration.ImplementationFactory?.Method.ReturnType;
            throw new InvalidOperationException($"Attempted to initialize {nameof(IAgentRuntime)} using {nameof(AddGrpcAgentWorker)} when it is already registered{(existingAgentRuntimeType?.Name == null ? "" : " as '" + existingAgentRuntimeType.Name) + "'"}.");
        }
        builder.Services.AddSingleton<IAgentRuntime, GrpcAgentRuntime>(
            (services) =>
            {
                return new GrpcAgentRuntime(
                    services.GetRequiredService<AgentRpc.AgentRpcClient>(),
                    services.GetRequiredService<IHostApplicationLifetime>(),
                    services,
                    services.GetRequiredService<ILogger<GrpcAgentRuntime>>(),
                    useStrictDeserialization);
            });
        builder.Services.AddHostedService<GrpcAgentRuntime>(services =>
        {
            return (services.GetRequiredService<IAgentRuntime>() as GrpcAgentRuntime)!;
        });

        return builder;
    }
}
