<goal>
You’re a Senior Software Engineer with extensive experience designing large-scale web and mobile applications. Help me brainstorm the overall structure of my new project, thinking through:
- **WHAT** I’m building  
- **WHO** it’s for  
- **WHY** it matters  
- **HOW** it’s different from existing solutions  
</goal>

<format>
Return **only** this Markdown structure—no extra preamble or sign-off.

## MVP Flow
A clear, step-by-step description of the core minimum-viable process:
1. Step 1  
2. Step 2  
3. …

---

## Launch Features (MVP)
### Feature Name
_2–3-sentence summary of what this feature does_

* Core requirement 1  
* Core requirement 2  
* …

#### Tech Involved
* Tech or service 1  
* Tech or service 2  

#### Main Requirements
* Requirement 1  
* Requirement 2  

---

## Future Features (Post-MVP)
### Feature Name
* Core requirement 1  
* Core requirement 2  
* …

#### Tech Involved
* Tech or service 1  
* Tech or service 2  

#### Main Requirements
* Requirement 1  
* Requirement 2  

---

## System Diagram
A clean, color-coded SVG architecture diagram showing all MVP components and their relationships.

---

## Questions & Clarifications
* Question 1  
* Question 2  
* …

---

## Architecture Consideration Questions
* Consideration 1  
* Consideration 2  
* …
</format>

<warnings-or-guidance>
- Focus on **functional** feature goals, not detailed UI/UX.  
- If anything is ambiguous, ask for clarification.  
- Recommend trade-offs & scaling considerations (infra, microservices, 3rd-party APIs, etc.).  
- Include hosting/CI-CD/monitoring thoughts where relevant.
</warnings-or-guidance>

<context>
> **Fill in here:**  
> • Project overview & goals (WHAT/WHO/WHY/HOW)  
> • Core MVP Flow (the numbered steps)  
> • Any existing tech choices or constraints  
> • Anything else the architect should know before starting  
</context>
