// Copyright (c) Microsoft Corporation. All rights reserved.
// GrpcAgentRuntimeTests.cs

using FluentAssertions;
using Microsoft.AutoGen.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.AutoGen.Core.Grpc.Tests;

[Trait("Category", "GRPC")]
public class GrpcAgentRuntimeTests : TestBase
{
    private static readonly ILogger<GrpcAgentRuntimeTests> _logger = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Debug);
    }).CreateLogger<GrpcAgentRuntimeTests>();

    [Fact]
    public void AddGrpcAgentWorker_ShouldThrow_WhenIAgentRuntimeAlreadyRegistered()
    {
        // Arrange
        var builder = new AgentsAppBuilder();
        builder.Services.AddSingleton<IAgentRuntime, InProcessRuntime>(); // Simulate an existing registration

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            builder.AddGrpcAgentWorker("http://localhost:50051");
        });

        _logger.LogDebug("Exception message: {Message}", exception.Message);

        Assert.Contains("Attempted to initialize " + nameof(IAgentRuntime), exception.Message);
        Assert.Contains("using " + nameof(AgentsAppBuilderExtensions.AddGrpcAgentWorker), exception.Message);
        Assert.Contains("when it is already registered as '" + nameof(InProcessRuntime) + "'", exception.Message);
    }

    [Fact]
    public void AddGrpcAgentWorker_ShouldRegisterIAgentRuntime_WhenNoExistingRegistration()
    {
        // Arrange
        var builder = new AgentsAppBuilder();

        // Act
        builder.AddGrpcAgentWorker("http://localhost:50051");

        // Assert
        var serviceDescriptor = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(IAgentRuntime));
        Assert.NotNull(serviceDescriptor);
        Assert.Equal(typeof(GrpcAgentRuntime), serviceDescriptor.ImplementationFactory?.Method.ReturnType);
        Assert.True(serviceDescriptor.Lifetime == ServiceLifetime.Singleton, $"Expected {nameof(IAgentRuntime)} to be registered as Singleton.");
    }

    [Fact]
    public async Task GrpcRequestCollector_ShouldBeIsolatedBetweenTests()
    {
        // Arrange
        var fixture1 = new GrpcAgentRuntimeFixture();
        var fixture2 = new GrpcAgentRuntimeFixture();

        // Act
        var runtime1 = (GrpcAgentRuntime)await fixture1.StartAsync(startRuntime: true, registerDefaultAgent: false);
        var runtime2 = (GrpcAgentRuntime)await fixture2.StartAsync(startRuntime: true, registerDefaultAgent: false);

        await runtime1.RegisterAgentFactoryAsync("Agent1", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new TestProtobufAgent(id, runtime, new Logger<BaseAgent>(new LoggerFactory())));
        });

        await runtime2.RegisterAgentFactoryAsync("Agent2", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new TestProtobufAgent(id, runtime, new Logger<BaseAgent>(new LoggerFactory())));
        });

        // Assert
        fixture1.GrpcRequestCollector.RegisterAgentTypeRequests.Should().ContainSingle(r => r.Type == "Agent1");
        fixture2.GrpcRequestCollector.RegisterAgentTypeRequests.Should().ContainSingle(r => r.Type == "Agent2");

        // Cleanup
        fixture1.Dispose();
        fixture2.Dispose();
    }

    [Fact]
    public async Task GatewayShouldNotReceiveRegistrationsUntilRuntimeStart()
    {
        var fixture = new GrpcAgentRuntimeFixture();
        var runtime = (GrpcAgentRuntime)await fixture.StartAsync(startRuntime: false, registerDefaultAgent: false);

        Logger<BaseAgent> logger = new(new LoggerFactory());

        await runtime.RegisterAgentFactoryAsync("MyAgent", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new SubscribedProtobufAgent(id, runtime, logger));
        });
        await runtime.RegisterImplicitAgentSubscriptionsAsync<SubscribedProtobufAgent>("MyAgent");

        _logger.LogInformation("Before starting runtime: RegisteredAgentTypeRequests = {Count}, AddSubscriptionRequests = {Count}",
            fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Count,
            fixture.GrpcRequestCollector.AddSubscriptionRequests.Count);

        fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Should().BeEmpty();
        fixture.GrpcRequestCollector.AddSubscriptionRequests.Should().BeEmpty();

        await fixture.AgentsApp!.StartAsync().ConfigureAwait(true);

        _logger.LogInformation("After starting runtime: RegisteredAgentTypeRequests = {Count}, AddSubscriptionRequests = {Count}",
            fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Count,
            fixture.GrpcRequestCollector.AddSubscriptionRequests.Count);

        var registeredAgentTypes = fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Select(r => r.Type).ToList();
        logger.LogInformation($"Agents registered: {string.Join(", ", registeredAgentTypes)}");
        fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Should().NotBeEmpty();
        fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Single().Type.Should().Be("MyAgent");
        fixture.GrpcRequestCollector.AddSubscriptionRequests.Should().NotBeEmpty();

        fixture.GrpcRequestCollector.Clear();

        await runtime.RegisterAgentFactoryAsync("MyAgent2", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new TestProtobufAgent(id, runtime, logger));
        });

        fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Should().NotBeEmpty();
        fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Single().Type.Should().Be("MyAgent2");
        fixture.GrpcRequestCollector.AddSubscriptionRequests.Should().BeEmpty();

        fixture.Dispose();
    }

    [Fact]
    public async Task AgentAndTopicRegistrations_ShouldBeClassifiedCorrectly()
    {
        // Arrange
        var fixture = new GrpcAgentRuntimeFixture();
        var runtime = (GrpcAgentRuntime)await fixture.StartAsync(startRuntime: true, registerDefaultAgent: false);

        // Act
        await runtime.RegisterAgentFactoryAsync("TestAgent", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new TestProtobufAgent(id, runtime, new Logger<BaseAgent>(new LoggerFactory())));
        });

        var subscription = new TypeSubscription("TestTopic", "TestAgent");
        await runtime.AddSubscriptionAsync(subscription);

        // Assert
        fixture.GrpcRequestCollector.RegisterAgentTypeRequests.Should().ContainSingle(r => r.Type == "TestAgent");
        fixture.GrpcRequestCollector.AddSubscriptionRequests.Should().ContainSingle(r => r.Subscription.TypeSubscription.TopicType == "TestTopic");

        // Cleanup
        fixture.Dispose();
    }

    [Fact]
    public async Task Registrations_ShouldBeIsolatedBetweenTests()
    {
        // Arrange
        var fixture1 = new GrpcAgentRuntimeFixture();
        var fixture2 = new GrpcAgentRuntimeFixture();

        // Act
        var runtime1 = (GrpcAgentRuntime)await fixture1.StartAsync();
        var runtime2 = (GrpcAgentRuntime)await fixture2.StartAsync();

        await runtime1.RegisterAgentFactoryAsync("Agent1", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new TestProtobufAgent(id, runtime, new Logger<BaseAgent>(new LoggerFactory())));
        });

        await runtime2.RegisterAgentFactoryAsync("Agent2", async (id, runtime) =>
        {
            return await ValueTask.FromResult(new TestProtobufAgent(id, runtime, new Logger<BaseAgent>(new LoggerFactory())));
        });

        // Assert
        fixture1.GrpcRequestCollector.RegisterAgentTypeRequests.Should().ContainSingle(r => r.Type == "Agent1");
        fixture2.GrpcRequestCollector.RegisterAgentTypeRequests.Should().ContainSingle(r => r.Type == "Agent2");

        // Cleanup
        fixture1.Dispose();
        fixture2.Dispose();
    }
}
