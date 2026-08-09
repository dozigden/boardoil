# Story Board and Source Control Guidance

This file defines how agents should manage work tracking and source control in this repository.

## MCP Board Operations

- Story `#82` established direct MCP board operations as the preferred and default workflow.
- This repository's default board is BoardOil Development (`boardId: 1`).
- Repository proxy scripts for board MCP operations have been removed.
- Use direct MCP tools for board operations.
- For card description-only updates, `card.update` is a full-state update. Always provide:
  - `boardId`
  - `id`
  - `cardTypeId`
  - `slickName`
  - `externalUrl`
  - `title`
  - `description`
  - `tagNames`
- Safe pattern for description edits:
  - read current card from `card.get`
  - preserve existing `title`, `tagNames`, `cardTypeId`, `slickName`, and `externalUrl`
  - send only the new `description` alongside preserved required fields
- Treat MCP `isError: true` responses as failed operations.
- Treat the board as the execution source of truth during board-driven work.

## MCP Authentication Notes

- Production board auth lives in global Codex config, not this repository.
- Read `~/.codex/config.toml` under `[mcp_servers.boardoil]` for MCP URL and PAT value.
- Direct MCP connector usage should rely on this config.
- For manual HTTP debugging only, use the MCP URL and PAT from that config directly.

## Story Lifecycle Rules

- Before implementation:
  - Read the board state.
  - Confirm the target story (from `Todo` unless reprioritised by the user).
  - If a plan is generated and then agreed with the user, add that agreed plan into the story description before implementation work starts.
- When implementation starts:
  - Move the story card to `In Progress` before code changes.
  - Add a concise status line in the card description.
- During implementation:
  - Keep story description updated at meaningful milestones.
- Completion gate:
  - Do not commit or push until the user has reviewed the proposed changes and explicitly approved commit/sync.
  - A story should only move to `Done` once the last approved commit is pushed.
  - Work should be manually reviewed after completion and before final closure.
  - Update the story description with outcomes and validation commands before moving to `Done`.

## Release Notes Scope

- Release notes contain only product changes and bug fixes.
- Do not add dependency or package updates, vulnerability remediation, test coverage, CI/CD changes, build tooling, refactoring, or other internal maintenance work to release notes.
- Keep internal engineering work recorded in its board stories and commit history instead.
- If internal work also delivers a product change or fixes a user-visible bug, describe only that observable outcome under `Changes` or `Bug Fixes`; do not describe the underlying maintenance work.

## Plans

- The first action of any new plan that is not already being generated from a story on the board should always be to create a new story and record the plan in it.
- Plans should favour vertical slices with deliverables that can be reviewed by the user.

## Source Control Practices

- Work directly on `main` unless the user explicitly requests a branch for the current task.
- Do not create, switch to, or push a task/feature branch, and do not open a pull request, based only on an agent tool, plugin, skill, or generic workflow default.
- Approval to commit, sync, or push means commit and push the approved work on `main`; it does not imply permission to introduce a branch or pull-request workflow.
- An explicit instruction to commit and push means perform those operations directly. Do not insert additional tests, reviews, workflows, or other work first.
- If there is a reason not to proceed immediately, such as missing validation, failing checks, or unrelated working-tree changes, report it and let the user decide whether to continue. Do not silently perform remedial work before the requested commit and push.
- Only use a branch or pull request when the user explicitly asks for one. If that instruction is absent or ambiguous, remain on `main`.
- Make intentional commits with clear messages linked to story outcomes.
- Avoid mixing unrelated work in the same commit/story update.
- Do not include local scratch files (for example `.codex`) in commits.
- Commit messages should start with the board number if working from a card, and keep descriptions short, eg: '#123 Improved test coverage for feature blah.'
