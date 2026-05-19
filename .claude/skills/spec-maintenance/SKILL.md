---
name: spec-maintenance
description: Use when updating module specifications after implementing features or applying design changes — helps read the current specification, identify changes, and update to the current state
---

# Specification Maintenance

Use this skill after implementing a feature or applying a design change to update the corresponding module specification in `docs/specs/`.

## Workflow

1. **Identify the module specification** to update based on the changed domain (e.g., Skills -> `docs/specs/skills.md`)
2. **Read the current specification** to understand what is documented
3. **Read the design document or code changes** that triggered the update — understand what changed
4. **Update the specification** to reflect the current state:
   - Add new entities, endpoints, or behaviors
   - Modify existing entries that changed
   - Remove anything that was deleted or replaced
5. **Check the ~300 line limit** — if exceeded, condense tables or merge related behaviors
6. **Verify that no history or rationale has leaked through** — specifications describe only the current state, never "was changed from X to Y" or "this was added because..."
7. **Verify that the template structure** is preserved (see below)

## Rules

- **Current state only** — no history, rationale, references to design documents or PRs
- **No implementation details** — don't mention MediatR, FluentValidation, EF Core, or internal patterns
- **Maximum ~300 lines** per module specification — use tables for structured data, bullets for behaviors
- **Tables for data model and endpoints** — bullets for key behaviors
- **Reference code files** for complex logic instead of re-explaining it

## Module Specification Template

```markdown
# Module Name

Brief purpose (2-3 sentences).

## Data Model

| Entity | Key Fields | Notes |
|--------|-----------|-------|

## API Endpoints

| Method | Path | Authorization | Description |
|--------|------|---------------|-------------|

## Key Behaviors

- Bulleted list of important business rules and flows

## Content Structure

(Only for MDX-based modules — describes file location and frontmatter)

## Admin Operations

(Brief description of admin-specific features, if any)
```

## Section Guidelines

**Data model table:**
- One row per entity
- Key fields: list important fields (Id, Slug, FK, status fields, timestamps)
- Notes: constraints, relationships, enum values

**API endpoints table:**
- Group by authorization level or feature area if the module is large (use subheadings)
- Authorization column: Anonymous, Authenticated, Admin, Owner/Admin, Instructor/Admin
- Description: one-line description of what the endpoint does

**Key behaviors:**
- Business rules and invariants
- State transitions
- Idempotency guarantees
- Automatically triggered side effects (e.g., "completing a section automatically completes the skill")
- Access control nuances not reflected in the endpoints table

**Content structure** (if applicable):
- MDX file location and path conventions
- What is stored in the DB vs. in MDX
- Custom MDX components used

**Admin operations:**
- Brief description of admin-specific workflows
- Constraints (e.g., "cannot delete if user data exists")
