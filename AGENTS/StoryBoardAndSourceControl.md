# Story Board and Source Control Guidance

This file defines how agents should manage work tracking and source control in this repository.

## MCP Board Operations

- Story `#82` established direct MCP board operations as the preferred and default workflow.
- Repository proxy scripts for board MCP operations have been removed.
- Use direct MCP tools for board operations.
- For card description-only updates, `card.update` is a full-state update. Always provide:
  - `boardId`
  - `id`
  - `cardTypeId`
  - `title`
  - `description`
  - `tagNames`
- Safe pattern for description edits:
  - read current card from `board.get`
  - preserve existing `title`, `tagNames`, and `cardTypeId`
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

## Plans

- The first action of any new plan that is not already being generated from a story on the board should always be to create a new story and record the plan in it.
- Plans should favour vertical slices with deliverables that can be reviewed by the user.

## Source Control Practices

- Make intentional commits with clear messages linked to story outcomes.
- Avoid mixing unrelated work in the same commit/story update.
- Do not include local scratch files (for example `.codex`) in commits.
- Commit messages should start with the board number if working from a card, and keep descriptions short, eg: '#123 Improved test coverage for feature blah.'
