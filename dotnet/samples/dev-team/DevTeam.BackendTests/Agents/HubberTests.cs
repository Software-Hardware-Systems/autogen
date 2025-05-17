// Copyright (c) Microsoft Corporation. All rights reserved.
// HubberTests.cs

using DevTeam.Backend.Agents;
using DevTeam.Backend.Services;
using Microsoft.AutoGen.Contracts;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DevTeam.Tests;

public class HubberTests
{
    [Fact]
    public void ExtractDetailsFromTopicSource_ParsesOrgRepoWithDashes()
    {
        var mockGithubService = new Mock<IManageGithub>();
        var agentId = new AgentId("Hubber", "test-key");
        var mockRuntime = new Mock<IAgentRuntime>();
        var logger = new LoggerFactory().CreateLogger<Hubber>();

        var hubber = new Hubber(
            mockGithubService.Object,
            agentId,
            mockRuntime.Object,
            logger);

        var topicId = new TopicId("Skill", "Org=org-with-dash|Repo=repo-with-dash|IssueNumber=123|ParentIssueNumber=456");
        var (org, repo, issueNumber, parentIssueNumber) = hubber.GetType()
            .GetMethod("ExtractDetailsFromTopicSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(hubber, new object[] { topicId }) as ValueTuple<string, string, long, long>? ?? default;

        Assert.Equal("org-with-dash", org);
        Assert.Equal("repo-with-dash", repo);
        Assert.Equal(123, issueNumber);
        Assert.Equal(456, parentIssueNumber);
    }

    [Fact]
    public void ExtractDetailsFromTopicSource_ParsesWithoutParentIssueNumber()
    {
        var mockGithubService = new Mock<IManageGithub>();
        var agentId = new AgentId("Hubber", "test-key");
        var mockRuntime = new Mock<IAgentRuntime>();
        var logger = new LoggerFactory().CreateLogger<Hubber>();

        var hubber = new Hubber(
            mockGithubService.Object,
            agentId,
            mockRuntime.Object,
            logger);

        var topicId = new TopicId("Skill", "Org=org|Repo=repo|IssueNumber=789");
        var (org, repo, issueNumber, parentIssueNumber) = hubber.GetType()
            .GetMethod("ExtractDetailsFromTopicSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(hubber, new object[] { topicId }) as ValueTuple<string, string, long, long>? ?? default;

        Assert.Equal("org", org);
        Assert.Equal("repo", repo);
        Assert.Equal(789, issueNumber);
        Assert.Equal(0, parentIssueNumber);
    }
}
