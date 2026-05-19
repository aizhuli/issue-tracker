---
name: requirements
description: Use when creating a requirements specification based on MVP Scope. Structured exploration of functional and non-functional requirements, constraints, and dependencies.
---

# Requirements: Requirements Specification

## Goal

Turn the MVP Scope into a formal requirements specification with clear functional and non-functional requirements, constraints, and dependencies through a structured dialogue.

## Inputs

Before starting, read `docs/PROJECT_BRIEF.md` and `docs/MVP_SCOPE.md`. If any file is not found — notify the user and suggest creating the missing document first.

## Process

### Phase 1: Understanding Validation

Briefly summarize the key scenarios from the MVP Scope (3-5 sentences) and ask:

1. "Do I understand the priorities correctly? Has anything changed since the MVP Scope was written?"

### Phase 2: Functional Requirements by Scenario

For each scenario from the MVP Scope, ask ONE question at a time. Wait for an answer before the next one.

2. "Let's take the [name] scenario. What data does the system receive as input? What does it return as output?"
3. "What validations and checks are needed? What counts as an error and how should the system respond?"
4. "Are there business rules that constrain behavior? For example: limits, access rights, required fields?"
5. "Does the system need to maintain a history of actions or states? What exactly and for how long?"

Repeat questions 2-5 for each critical scenario. Suggest your own requirement options based on typical practices if the user has difficulty.

### Phase 3: Non-Functional Requirements

6. "What response time is acceptable for the main operations? For example: page load, sending a message to an agent, loading a dashboard?"
7. "How many users or concurrent operations should the system handle in the MVP?"
8. "Are there security and authorization requirements? Who can see and do what?"
9. "Which data is critical and must not be lost? Are backups needed in the MVP?"

### Phase 4: Edge Cases

10. "What happens if [external service] is unavailable? How should the system behave?"
11. "What if the user does something unexpected — closes the tab mid-process, submits an empty form, opens the same project in two tabs?"

### Phase 5: Requirement Dependencies

12. "Which requirements block each other? What needs to be implemented first to make the rest possible?"

### Phase 6: Document Formation

When all answers are collected — present the Requirements in sections of 200-300 words each. After each section ask: "Does this part look right?" Be prepared to go back and refine if something doesn't add up.

## Output

After validating all sections, create the file `docs/REQUIREMENTS.md`:

```markdown
# Requirements: [Project Name]

## Overview

[Brief description of the requirements scope and relationship to the MVP Scope. 2-3 sentences]

## Functional Requirements

### FR-1: [Functional Area Name]

**Related Scenario:** [Reference to the scenario from MVP Scope]

| ID | Requirement | Priority |
|---|---|---|
| FR-1.1 | [The system shall...] | Must / Should / Could |
| FR-1.2 | [The system shall...] | Must / Should / Could |

### FR-N: [Functional Area Name]

[Same structure]

## Non-Functional Requirements

### Performance

| ID | Requirement | Metric |
|---|---|---|
| NFR-1 | [Description] | [Specific value] |

### Security

| ID | Requirement | Metric |
|---|---|---|
| NFR-N | [Description] | [Specific value] |

### Reliability

| ID | Requirement | Metric |
|---|---|---|
| NFR-N | [Description] | [Specific value] |

## Constraints and Assumptions

**Constraints:**
- [Technical, business, or organizational constraints]

**Assumptions:**
- [What we consider true without verification]

## Requirement Dependencies

[Which requirements block others. Recommended order of implementation]

## Glossary

| Term | Definition |
|---|---|
| [Term] | [What it means in the project context] |
```

## Key Principles

- **One question at a time** — don't overwhelm the conversation partner with multiple questions
- **Options are preferred** — offer 2-3 options, they are easier to respond to
- **"The system shall" — requirement format** — each requirement is formulated as a statement about system behavior
- **Verifiability** — every requirement must be verifiable: with a specific metric or criterion
- **Challenge implicit requirements** — "work fast" → "response time < 500ms at the 95th percentile"
- **Don't invent requirements** — extract requirements from MVP Scope scenarios, don't create new ones
- **Incremental validation** — present the document in sections, validate each one
- **Be flexible** — go back and refine earlier decisions if something doesn't add up
