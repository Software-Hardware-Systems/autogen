// Copyright (c) Microsoft Corporation. All rights reserved.
// Consts.cs

namespace DevTeam.Backend;

public class SkillPersona
{
    public const string DevTeam = "DevTeam";
    // Agents that incorporate AI
    public const string Stakeholder = "Stakeholder";
    public const string ProductOwner = "ProductOwner";
    public const string DeveloperLead = "DevLead";
    public const string Developer = "Dev";
    // Agents that do not incorporate AI
    public const string Hubber = "Hubber";
    public const string AzureGenie = "AzureGenie";
    public const string Sandbox = "Sandbox";
}

public class StakeholderActivity
{
    // ToDo: Expound on these activities to embody the SCRUM stakeholder role
    public const string Ask = "Ask";
    public const string Answer = "Answer"; // guidance
    public const string Review = "Review"; 
    public const string Approve = "Approve";
}
