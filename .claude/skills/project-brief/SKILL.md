---
name: project-brief
description: Use when creating a Project Brief, defining a project idea, or clarifying scope. Structured exploration of the problem, user, solution, and MVP boundaries.
---

# Project Brief: Brainstorming

## Goal

Turn a vague project idea into a focused 2-3 page Project Brief through a structured dialogue.

## Process

### Phase 1: Problem Exploration

Ask questions ONE AT A TIME. Wait for an answer before the next question.

1. "Describe the situation: who is currently facing this problem and how do they cope without your solution?"
2. "What's bad about it? Why doesn't the current approach work?"
3. "How often does this happen? How painful is it on a scale of 1 to 10?"

If the user can't articulate the problem, ask: "Forget the solution. Tell me the story of a specific person who's struggling with something."

### Phase 2: User Clarification

4. "Who exactly is your user? Not 'all farmers', but which specific farmer?"
5. "In what context will they encounter your product? Where, when, on what device?"
6. "What has this person already tried? Why didn't it work?"

### Phase 3: Solution Definition

7. "Now tell me your solution. What does the system DO (not how it's built internally)?"
8. "The user opens the app. What do they do first? Second? What do they see as the result?"
9. "How is this better than how they cope now?"

### Phase 4: MVP Boundaries

10. "You have 2 weeks for the first version. What 2-3 functions are mandatory to solve the main problem?"
11. "What do you want to add but can defer for later?"
12. "Are there external dependencies or integrations without which the MVP doesn't work?"

### Phase 5: Success Criteria

13. "Imagine the product is ready. How will you show it works? Describe one concrete scenario from start to finish."
14. "How will you know the user is satisfied? What will they say or do?"

### Phase 6: Document Formation

When all answers are collected — present the Project Brief in sections of 200-300 words each. After each section ask: "Does this part look right?" Be prepared to go back and refine if something doesn't add up.

## Output

After validating all sections, create the file `docs/PROJECT_BRIEF.md`:

```markdown
# Project Brief: [Name]

## Problem

[Description of the current situation: who, what they do, why it's bad.
A concrete story or example. 4-5 sentences]

## User

[Who exactly, in what context, what experience.
What they've already tried, why it didn't work. 4-5 sentences]

## Solution

[What the product does for the user. The main usage scenario.
The key difference from existing alternatives. 5-6 sentences, no technical details]

## MVP Boundaries

**In scope:**
- [feature 1 — brief description]
- [feature 2 — brief description]
- [feature 3 — brief description]

**Out of scope (deferred):**
- [deferred 1]
- [deferred 2]

**Dependencies and integrations:**
- [if there are external APIs, services, data]

## Success Criteria

[Concrete scenario: user X does Y, gets Z.
How to know the goal is achieved. 3-4 sentences]
```

## Key Principles

- **One question at a time** — don't overwhelm the conversation partner with multiple questions
- **Options are preferred** — offer 2-3 options, they are easier to respond to
- **Specifics over abstractions** — require examples, names, numbers
- **Challenge vague answers** — "all users" → "who specifically?"
- **Problem first, solution second** — don't allow jumping to features until the problem is clear
- **YAGNI without compromise** — ruthlessly cut unnecessary things from the MVP
- **Incremental validation** — present the document in sections, validate each one
- **Be flexible** — go back and refine earlier decisions if something doesn't add up
