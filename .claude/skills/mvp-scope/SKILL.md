---
name: mvp-scope
description: Use when detailing MVP Scope based on the Project Brief. Structured exploration of user scenarios, priorities, and readiness criteria.
---

# MVP Scope: Detailing

## Goal

Turn the Project Brief into a detailed MVP Scope with concrete user scenarios, priorities, and readiness criteria through a structured dialogue.

## Inputs

Before starting, read `docs/PROJECT_BRIEF.md`. If the file is not found — notify the user and suggest creating the Project Brief first.

## Process

### Phase 1: Understanding Validation

Briefly summarize the essence of the Project Brief (3-4 sentences) and ask:

1. "Do I understand the project correctly? Has anything changed since the Brief was written?"

### Phase 2: Decomposing Features into Scenarios

For each feature from the "In scope" section of the Project Brief, ask ONE question at a time. Wait for an answer before the next one.

2. "Let's take [feature]. Describe a concrete scenario: the user wants [goal] — what steps do they go through from start to result?"
3. "Are there any alternative paths in this scenario? What if [option A] or [option B]?"
4. "What is the minimum version of this scenario that still provides value? What can be simplified, and what cannot?"

Repeat questions 2-4 for each feature. Suggest your own scenario options if the user has difficulty.

### Phase 3: Prioritization

Once all scenarios are collected:

5. "Here are all the scenarios we've discussed: [list]. Which are critical for the first launch, and which can be added iteratively?"
6. "If you could show the MVP with only three scenarios — which ones would those be?"
7. "Are there technical or business constraints that affect the order of implementation?"

### Phase 4: Readiness Criteria

8. "For each scenario — how will you know it's implemented? What specifically should the user see or get?"
9. "Are there any non-functional requirements that are critical for the MVP? For example: response time, number of concurrent users, offline operation?"

### Phase 5: Risks and Dependencies

10. "Which scenario seems most technically challenging? Why?"
11. "Are there external dependencies that could block implementation?"

### Phase 6: Document Formation

When all answers are collected — present the MVP Scope in sections of 200-300 words each. After each section ask: "Does this part look right?" Be prepared to go back and refine if something doesn't add up.

## Output

After validating all sections, create the file `docs/MVP_SCOPE.md`:

```markdown
# MVP Scope: [Project Name]

## Overview

[Brief description of the MVP goal and key user value. 2-3 sentences]

## User Scenarios

### Scenario 1: [Name]

**Priority:** Critical / Important / Desirable

**Description:** [What the user wants to do and why. 1-2 sentences]

**Main Flow:**
1. [Step 1]
2. [Step 2]
3. [Step N]

**Alternative Paths:**
- [If condition X — then Y]

**Readiness Criterion:** [How to know the scenario is implemented]

### Scenario N: [Name]

[Same structure]

## Non-Functional Requirements for MVP

- [Requirement 1 — specific metric or constraint]
- [Requirement N]

## Simplifications and Assumptions

[What is deliberately simplified in the MVP compared to the full vision.
What assumptions are made. 3-5 bullet points]

## Risks and Dependencies

| Risk / Dependency | Impact | Mitigation |
|---|---|---|
| [Description] | [High / Medium / Low] | [What we do] |

## Implementation Order

[Recommended sequence of scenario implementation with rationale.
Which scenarios can be done in parallel]
```

## Key Principles

- **One question at a time** — don't overwhelm the conversation partner with multiple questions
- **Options are preferred** — offer 2-3 options, they are easier to respond to
- **Specifics over abstractions** — require examples, steps, metrics
- **Challenge "I want everything"** — force prioritization, the MVP can't include everything
- **Scenarios over features** — the "dashboard" feature means nothing, but "the user sees the status of all modules" means something
- **Readiness criteria are mandatory** — every scenario must have a verifiable criterion
- **Incremental validation** — present the document in sections, validate each one
- **Be flexible** — go back and refine earlier decisions if something doesn't add up
