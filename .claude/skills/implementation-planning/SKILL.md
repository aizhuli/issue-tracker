---
name: implementation-planning
description: Use after finalizing the design to create a step-by-step implementation plan with checkboxes, optimized for maximum parallelism using Claude Code agent teams. The plan is a markdown file that a team lead agent executes by spawning team members.
---

# Implementation Planning

Create a step-by-step implementation plan from a design document, structured for execution by a Claude Code agent team.

## Context: How Agent Teams Work

Claude Code agent teams consist of a **team lead** (main session) and **team members** (spawned Claude Code instances). Key properties:

- All team members share **one working directory** (no isolation)
- Team members have their own context windows but load the same CLAUDE.md, skills, and MCP servers
- Team members communicate via a **shared task list** and **direct messages**
- Tasks have **dependencies** — blocked tasks wait for their dependencies to complete
- Team members **self-assign** available tasks or receive them from the lead
- **File conflicts are the main risk**: two team members editing the same file will overwrite each other's changes

## When to Use

After the design document is written and approved. Before writing any implementation code.

## Process

### 1. Analyze the Design

- Read the design document carefully
- Identify all discrete units of work
- Build a dependency map between them (what blocks what)
- Identify which units touch backend, frontend, or both

### 2. Build the Dependency Graph

For each unit of work, identify:
- What it needs before it can start (inputs / preconditions)
- What it produces that others need (outputs)
- Whether it is backend-only, frontend-only, or cross-cutting

Units with no dependencies between them go into the same phase (parallel).

### 3. Write the Plan

Create `docs/plans/YYYY-MM-DD-<topic>.md` with the structure below.

#### Plan Structure

```markdown
# Implementation Plan: <Topic>

Design: `docs/designs/YYYY-MM-DD-<topic>.md`

## Phase 1 — <Phase Name>

*No dependencies. All team members work in parallel.*

### Task 1.1: <Short Task Name>
- [ ] Step 1 description
- [ ] Step 2 description
- [ ] Step 3 description

**Files:** `path/to/file1.ts`, `path/to/file2.cs`

### Task 1.2: <Short Task Name>
- [ ] Step 1 description
- [ ] Step 2 description

**Files:** `path/to/file1.ts`, `path/to/file2.ts`

## Phase 2 — <Phase Name>

*Depends on: Phase 1 (reason)*

### Task 2.1: <Short Task Name>
- [ ] Step 1 description
- [ ] Step 2 description

**Files:** `path/to/file.ts`

...
```

### 4. Execution Model

The plan is designed for execution by the **team lead**:

1. **Lead creates the agent team** and a shared task list from the plan
2. **Lead spawns team members** — one per parallel task in the current phase (3–5 team members recommended)
3. **Team members self-assign tasks** from the shared list and work through the checkboxes
4. **Tasks have dependencies** — Phase 2 tasks depend on Phase 1 tasks, so they remain blocked until Phase 1 completes
5. **Lead monitors progress**, redirects team members as needed, and synthesizes results
6. **Lead updates checkboxes** in the plan file as tasks complete
7. **When all tasks in a phase are done**, dependent tasks in the next phase unlock automatically
8. **Lead disbands the team** when all phases are complete

The lead should tell team members to follow relevant project skills (e.g., `vertical-slice-architecture`, `component-testing`) when assigning backend work.

## Plan Writing Rules

### Maximize Parallelism
- Fewer phases is better — move units of work to earlier phases when dependencies allow
- Backend and frontend work on different files, so they almost always parallelize
- Two team members can work on different frontend pages in parallel (different files)
- Two team members CANNOT safely edit the same file — combine those units into one task or split them into different phases

### Task Scope Rules
- Each task is a cohesive, self-contained unit of work for one team member
- A task should touch a focused set of files — list them in the **Files:** section
- If two tasks will edit the same file, combine them into one task or separate them into different phases
- Prefer small tasks (3–8 checkboxes) over large ones — easier to review and restart on failure
- Aim for 5–6 tasks per team member to keep them productive

### Checkpoint Granularity
- Each checkbox = one verifiable action (create a file, add a function, update a config, run a test)
- Too granular (every line of code) = noise; too coarse ("implement the feature") = useless
- Good: "Add `generateMetadata` to `/skills/[slug]/page.tsx` with title, description, OG image, canonical URL"
- Bad: "Update the skills page" (too vague) or "Add og:title line" (too granular)

### File Conflict Prevention
- **List all files each task will touch** in the **Files:** section
- Before finalizing the plan, verify no two tasks in the same phase share files
- Shared utilities / types needed by multiple tasks should be created in an earlier phase
- If a shared file must be edited by multiple tasks, combine them into one task or split them into different phases

### Team Size Recommendations
- 3–5 team members for most work — balances parallelism against coordination overhead
- Each team member can sequentially work through multiple tasks (self-assigning from the list)
- More team members ≠ faster — coordination cost grows, returns diminish past 5–6
