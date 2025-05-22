# AI Board of Directors - System Memory

This document captures the key architectural components, design decisions, and implementation details for the AI Board of Directors system.

## System Overview

The AI Board of Directors is a semi-autonomous AI agent ecosystem that mirrors traditional business organizational structures. It helps solo entrepreneurs manage complex projects through AI-powered collaboration organized in familiar business hierarchies (CEO, CTO, HR, etc.). The system leverages Microsoft technologies (Azure AI Foundry, Semantic Kernel, Aspire, GitHub, MCP) and follows SCRUM methodologies for implementation.

## Key Components

### Board Members
- **CEO**: Overall strategic leadership and final decision-making authority
- **CTO**: Technical strategy and oversight of implementation details
- **CFO**: Resource allocation and budget considerations

### DevTeam
- **Architect**: System design and technical architecture decisions
- **Scrum Master**: Process management and impediment removal
- **Product Owner**: Requirements management and prioritization
- **DevLead**: Technical leadership and code quality oversight
- **Developers**: Implementation of features and functionality

## Architecture Decisions

### Authentication
- Restricted to GitHub/Azure providers for MVP
- Future expansion to include additional authentication methods

### Conflict Resolution
- Follows organizational hierarchy with final escalation to humans
- Incorporates input from diversity specialist for conflict detection/resolution

### Work Management

- Supports asynchronous work with extended conversation pauses
- High customization allowed for all agent roles
- Conversation threads serve as the basis for checkpoints and metrics
- System designed to recover from interruptions and outages

### Integration

- MCP used for all external system integrations
- Checkpoints and metrics based on conversation threads

### Information Management

- Separates "How to think" (company IP) from "What to think about" (client data)
- Local-to-cloud context management strategy for data handling

## Success Metrics

- Comparison of AI-generated artifacts with human-generated ones
- Monitoring approaches that model human organizational monitoring

## Future Considerations

- Multi-tenant infrastructure for supporting multiple users/organizations
- Enhanced AIGent resources for improved performance and capabilities
- Backup and conversation persistence strategies

## File Structure

This section documents important files in the system and their purposes:

- `.github/prompts/scrum.stakeholder.prompt.md`: SCRUM Stakeholder prompt template
- `.github/prompts/scrum.productmanager.prompt.md`: SCRUM Product Manager prompt template
- `.github/prompts/kochel.architect.prompt.md`: Architect prompt template
- `backlog/sprint_0_product_backlog.csv`: Product backlog for Sprint 0
- `backlog/sprint_0_memory_dump.txt`: Planning session memory dump
- `dotnet/samples/dev-team/devteam.backend/agents`: Prototype DevTeam implementations
- `.vscode/mcp.json`: MCP configuration file
- `docs/memory.md`: This file - documentation of system architecture and memory
- `docs/ai_board_prd.md`: Product Requirements Document for the AI Board of Directors system
