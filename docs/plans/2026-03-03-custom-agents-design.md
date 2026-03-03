# Custom Development Agents — Design

## Overview

Three Claude Code custom slash commands (`/review`, `/scaffold`, `/privacy`) that encode project-specific knowledge into actionable development workflows. These complement the existing `/pm` product manager agent and generic superpowers skills.

## Problem

Generic development skills (TDD, code review, debugging) don't know project-specific conventions:
- PBKDF2 hashing for PII matching (not encryption, not bcrypt)
- Test boilerplate quirks (`Guid.NewGuid()` outside the lambda, `RemoveAll` for `AddHttpClient` services)
- The PII field inventory and intentional removal of `SenderEmail`
- Service lifetime rules tied to dependency chains

Developers (human or AI) working on this project need these conventions enforced automatically.

## Approach

Each agent is a single `.claude/commands/<name>.md` markdown file following the pattern established by `/pm`. Agents are invoked via `/review`, `/scaffold`, and `/privacy` in Claude Code.

## Agents

### `/review` — Pre-commit review

Checks staged or changed code against three principles before committing:
1. **Data protection** — new PII stored correctly? Hashing used for matching? No raw emails?
2. **Code quality** — thin controllers? Service pattern followed? Correct DI lifetime?
3. **Testing** — test exists? Boilerplate correct? Endpoint coverage?

Outputs a structured PASS/WARN/ISSUE report.

### `/scaffold` — Feature scaffolding

Generates skeleton code from templates derived from existing patterns:
- Entity from `OsmBooking` pattern
- Service from `ILinkingService`/`LinkingService` pattern
- Controller from `EmailsController` pattern
- Integration test from `EmailCaptureTests` pattern
- Unit test from `HashingServiceTests` pattern

Includes a privacy checklist for any new data being stored. Presents plan before generating.

### `/privacy` — PII audit

Scans code for data protection issues:
- Identifies PII fields in entities and DTOs
- Traces data flows from ingestion through storage to logging
- Compares against known baseline of accepted exceptions
- Outputs a privacy impact assessment

## What these agents DON'T replace

- `superpowers:test-driven-development` — still handles TDD workflow
- `superpowers:requesting-code-review` — still handles generic review
- `superpowers:systematic-debugging` — still handles debugging

The custom agents add project-specific checks that generic skills can't provide.

## CLAUDE.md updates

Three new sections added to CLAUDE.md:
- **Custom Commands** — documents all four slash commands
- **PII Field Inventory** — canonical table of entity/field/storage/purpose
- **Service Lifetimes** — reference for correct DI registration

## References

- Issue #9 — defines data protection, code quality, and testing viewpoints
- `docs/plans/2026-02-28-pm-agent-design.md` — design pattern for `/pm`
