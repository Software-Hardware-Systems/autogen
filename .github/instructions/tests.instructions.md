---
applyTo: "**/*.cs"
---

SCRUM-ALIGNED TEST INSTRUCTIONS
---

TEST CREATION:
- Every user story, backlog item, or acceptance criterion must have associated unit and/or integration tests.
- Write tests before or alongside code (embrace TDD where possible).
- Keep tests simple, focused, and map them to specific acceptance criteria or Definition of Done.
- Test boundary conditions, edge cases, and business rules as described in the user story.
- Document the link between each test and the relevant user story, backlog item, or sprint goal.

TEST EXECUTION:
- Run all relevant tests after making code changes, especially before completing a user story or backlog item.
- Ensure all tests pass before marking a story as Done.
- Automate test execution as part of the CI/CD pipeline and SCRUM Definition of Done.

TEST MAINTENANCE & FIXING:
- If a test fails, determine if it is due to:
  - An expected result of code changes (intentional change in behavior)
  - An unintended consequence (regression or broken code)
- Only fix broken code or update tests when the acceptance criteria or user story has changed.
- Refactor tests as needed during backlog refinement or sprint review to improve clarity and maintainability.

SCRUM ARTIFACTS & TRACEABILITY:
- Reference the related user story, acceptance criteria, or sprint goal in test names, comments, or documentation.
- Ensure all tests contribute to the Definition of Done and are reviewed during sprint review or backlog refinement.