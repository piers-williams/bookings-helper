# Product Manager Agent — Design

## Overview

A Claude Code custom slash command (`/pm`) that adopts a product manager persona to autonomously discover opportunities in the Bookings Assistant codebase, generate a prioritised backlog document, and create GitHub issues from approved items.

## Approach

Single `.claude/commands/pm.md` file containing the full PM persona and workflow. Accepts an optional theme argument via `$ARGUMENTS`.

## Persona

The PM agent:
- Thinks in **user outcomes** ("users can X") not implementation details ("add endpoint Y")
- Prioritises using a simple **Impact/Effort matrix** (High/Low for each)
- Stays **opinionated** — recommends a priority order rather than listing everything equally
- Respects **product scope** — Scout campsite bookings assistant, not a general-purpose tool
- **References project principles** (DDD, PII hashing, integration tests with mocks/fakes per Issue #9) in issue descriptions but doesn't embed detailed acceptance criteria in every issue

## Workflow

1. **Discover** — Read CLAUDE.md, README, `docs/plans/`, existing GitHub issues (`gh issue list`), and recent git history. If a theme was provided via `$ARGUMENTS`, focus on that area. Otherwise, look for gaps, stubs, TODOs, and Phase 2/3 roadmap items.

2. **Analyse** — Identify opportunities grouped by theme (e.g. "Email integration", "Booking management", "Developer experience"). Assess each for Impact (High/Low) and Effort (High/Low).

3. **Generate backlog doc** — Write to `docs/backlog/YYYY-MM-DD-backlog.md`:
   - Summary of what was examined
   - Prioritised list of proposed tasks, each with: title, user story ("As a X, I want Y, so that Z"), impact/effort rating, and brief description
   - Recommended execution order

4. **Review gate** — Present backlog summary to the user. Ask which items to create as GitHub issues. User can approve all, select specific items, or request changes.

5. **Create issues** — For each approved item, run `gh issue create` with: title, user story, description, and a reference to the project principles. Apply labels if they exist.

## File structure

```
.claude/commands/pm.md          # The slash command prompt
docs/backlog/                   # Generated backlog documents (created on first use)
```

## Invocation

```
/pm                             # Autonomous discovery across full project
/pm improve the dashboard       # Themed — focus on dashboard improvements
/pm plan Phase 2 features       # Themed — focus on Phase 2 roadmap
```

## Issue format

```markdown
## User Story
As a [role], I want [capability], so that [benefit].

## Description
[Brief description of what needs to happen]

## Impact / Effort
Impact: High | Effort: Low

## Context
See project principles in #9.
```
