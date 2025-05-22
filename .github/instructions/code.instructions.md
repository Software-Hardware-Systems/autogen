---
applyTo: "**/*.cs"
---

SCRUM-ALIGNED CODE INSTRUCTIONS
---

GENERAL PRINCIPLES:
- All implementation and documentation should reflect SCRUM values: transparency, inspection, adaptation, collaboration, and delivering business value.
- Code and documentation should be easily understandable for experienced developers and SCRUM team members.
- The intent is to teach, explain, and guide experienced developers in a SCRUM context.

SCRUM CODE RULES:
- Avoid the use of var; always use descriptive and meaningful variable names.
- Write code in small, testable increments that map to user stories or backlog items.
- Each code change should reference the related user story, acceptance criteria, or sprint goal in comments or commit messages.
- Ensure code is modular and supports iterative development and refactoring.
- Use clear separation of concerns to facilitate backlog refinement and sprint planning.

SCRUM DOCS RULES:
- Every file must include file-level documentation explaining its purpose, scope, and how it fits into the overall SCRUM project (e.g., which epic, feature, or user story it supports).
- Each function/component must include:
  - Purpose and behavior
  - Inputs, outputs, and side effects
  - How it relates to acceptance criteria or user stories
- Inline comments should explain complex logic, business rules, or SCRUM-relevant decisions (e.g., why a story was split or a technical spike was needed).
- Document all types, interfaces, and data contracts, including their role in the SCRUM process.
- Note edge cases, error handling, and any SCRUM-related assumptions or limitations (e.g., dependencies on other backlog items).

SCRUM ARTIFACTS & TRACEABILITY:
- Reference the Product Backlog, Sprint Backlog, or Definition of Done where relevant.
- Link code and documentation to user stories, acceptance criteria, and sprint goals for traceability.

CONTINUOUS IMPROVEMENT:
- Encourage refactoring and technical debt reduction as part of each sprint.
- Document lessons learned and improvements in code comments or sprint review notes.