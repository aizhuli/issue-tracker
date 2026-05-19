---
name: design-brainstorming
description: "You MUST use this skill before any creative work — creating features, building components, adding functionality, or changing behavior. Explores user intent, requirements, and design before implementation."
---

# Turning Ideas into Designs

## Overview

Help turn ideas into complete designs and specifications through a natural collaborative dialogue.

Start by understanding the current project context, then ask questions one at a time to refine the idea. When you understand what you're building, present the design in small sections (200–300 words each), checking after each section that everything looks right.

## Process

**Understanding the idea:**
- First explore the current state of the project (files, documents, recent commits)
- Ask questions one at a time to refine the idea
- Prefer questions with answer options when possible, but open-ended questions are also acceptable
- Only one question per message — if the topic requires deeper exploration, break it into multiple questions
- Focus on understanding: goal, constraints, success criteria

**Exploring approaches:**
- Propose 2–3 different approaches with their trade-offs
- Present options in a conversational form with your recommendation and rationale
- Start with the recommended option and explain why

**Presenting the design:**
- When you believe you understand what you're building, present the design
- Break it into sections of 200–300 words
- After each section ask if everything looks right
- Cover: architecture, components, data flow, error handling, testing
- Be prepared to go back and refine if something doesn't make sense

## After the Design

**Documentation:**
- Record the validated design in `docs/designs/YYYY-MM-DD-<topic>.md`
- Commit the design document to git

## Key Principles

- **One question at a time** — don't overwhelm with multiple questions simultaneously
- **Prefer answer options** — they are easier to respond to than open-ended questions when possible
- **Ruthless YAGNI** — cut unnecessary features from all designs
- **Explore alternatives** — always propose 2–3 approaches before choosing
- **Incremental validation** — present the design in sections, validate each one
- **Be flexible** — go back and refine when something doesn't make sense
