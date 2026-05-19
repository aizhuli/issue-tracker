---
name: architecture
description: >
  Use when designing architecture based on Requirements. Structured exploration
  of components, stack, data model, API, and infrastructure.
---

# Architecture: Designing the Architecture

## Goal

Turn a requirements specification into an architecture document describing system components, their interactions, the tech stack, and the data model through a structured dialogue.

## Inputs

Before starting, read `docs/PROJECT_BRIEF.md`, `docs/MVP_SCOPE.md`, and `docs/REQUIREMENTS.md`. If any file is not found — notify the user and suggest creating the missing document first.

## Process

### Phase 1: Understanding Validation

Briefly summarize the key requirements and constraints (3-5 sentences) and ask:

1. "Do I understand the technical context correctly? Has anything changed?"

### Phase 2: Tech Stack

Ask questions ONE AT A TIME. Wait for an answer before the next one.

2. "Do you have any preferences for backend language and framework? For example: [option A], [option B], [option C] — based on the requirements, I'd recommend [option] because [reason]."
3. "For the frontend — any preferences? Given the responsiveness requirement, options are: [A], [B], [C]."
4. "Which database do you prefer? Given [the nature of the data from requirements], these would fit: [A], [B] — I recommend [option] because [reason]."
5. "Are there any additional infrastructure components needed? For example: task queue, cache, file storage?"

### Phase 3: Components and Their Interactions

6. "I see the following main system components: [list]. Does this breakdown make sense? What would you add or merge?"
7. "How do the components communicate? Options: [synchronous calls / message queue / events] — for [specific interaction] I recommend [option] because [reason]."
8. "Which external services need to be integrated? How should the system behave when they are unavailable?"

### Phase 4: Data Model

9. "Here are the main entities I see from the requirements: [list]. What are the relationships between them? What is missing?"
10. "Which data changes frequently, and which is nearly static? This affects storage and caching strategy."
11. "Is there any data that requires special handling? Tokens, keys, personal data?"

### Phase 5: API and Contracts

12. "Which API style do you prefer? REST, GraphQL, or a hybrid? For this project I recommend [option] because [reason]."
13. "What are the main endpoints needed to cover the scenarios from the MVP Scope?"

### Phase 6: Deployment and Infrastructure

14. "Where do you plan to host? Options: [A], [B], [C] — given the MVP scale I recommend [option]."
15. "Is CI/CD needed for the MVP? If so — what level: tests only, or a full pipeline through to deployment?"

### Phase 7: Document Formation

Once all answers are collected — present the Architecture in sections of 200-300 words each. After each section ask: "Does this part look right?" Be prepared to go back and refine if something doesn't add up.

## Output

After validating all sections, create the file `docs/ARCHITECTURE.md`:

```markdown
# Architecture: [Project Name]

## Overview

[High-level description of the architecture: application type, key architectural decisions,
relationship to requirements. 3-4 sentences]

## Tech Stack

| Component | Technology | Rationale |
|---|---|---|
| [Backend Language] | [Technology] | [Why chosen] |
| [Backend Framework] | [Technology] | [Why chosen] |
| [Frontend] | [Technology] | [Why chosen] |
| [Database] | [Technology] | [Why chosen] |
| [Infrastructure] | [Technology] | [Why chosen] |

## System Components

### [Component 1]

**Responsibility:** [What this component does]

**Interacts with:** [List of components]

**Key decisions:** [Why designed this way]

### [Component N]

[Same structure]

## Data Model

### [Entity 1]

| Field | Type | Description |
|---|---|---|
| [Field] | [Type] | [Purpose, if not obvious] |

**Relationships:** [Which entities it relates to and how]

### [Entity N]

[Same structure]

## API

### [Endpoint Group]

| Method | Path | Description |
|---|---|---|
| [GET/POST/...] | [/api/...] | [What it does] |

## Component Interactions

[Description of key data flows through the system.
How a request travels from the user through components to the result.
For complex flows — a textual description of the sequence of steps]

## Infrastructure and Deployment

**Hosting:** [Where and how]

**CI/CD:** [Pipeline, if any]

**Monitoring:** [What to track in the MVP]

## Decisions and Trade-offs

| Decision | Alternatives | Why chosen |
|---|---|---|
| [What was decided] | [What was considered] | [Arguments] |

## Project Structure

[Description of code organization: directories, modules, layers]
```

## Key Principles

- **One question at a time** — don't overwhelm the conversation partner with multiple questions
- **Options are preferred** — offer 2-3 options with rationale, they are easier to respond to
- **Recommend with arguments** — don't just offer options, say which one you recommend and why
- **Architecture follows requirements** — every decision should be justified by a specific requirement
- **Simplicity for the MVP** — don't design a system for a million users if the MVP is for ten
- **Challenge overengineering** — "do we need Kubernetes for the MVP?" — probably not
- **Document trade-offs** — every decision has a trade-off, record them
- **Incremental validation** — present the document in sections, validate each one
- **Be flexible** — go back and refine earlier decisions if something doesn't add up
