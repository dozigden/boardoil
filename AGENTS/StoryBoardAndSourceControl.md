# Story Board and Source Control Guidance

This file defines how agents should manage work tracking and source control in this repository.

## MCP Board Operations

- Story `#82` is an active workflow experiment to try direct MCP board operations instead of the repository proxy scripts.
- During this experiment:
  - direct MCP board operations are allowed and preferred
  - keep notes on `#82` about ergonomics, reliability, and whether this reduces elevation prompts
  - use scripts only if direct MCP is clearly blocked or materially worse for the case at hand
- Historical script entry points still exist:
  - `./scripts/board-mcp.sh` for general board operations
  - `./scripts/board-card-description-set-from-file.sh` for card description updates from a file
- Treat the board as the execution source of truth during board-driven work.

## MCP Authentication Notes

- Production board auth lives in global Codex config, not this repository.
- Read `~/.codex/config.toml` under `[mcp_servers.boardoil]` for MCP URL and PAT value.
- Pass explicit `--mcp-url` and `--token` to these scripts when environment variables are not set:
  - `board-mcp.sh`
  - `board-card-description-set-from-file.sh`

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

## Source Control Practices

- Make intentional commits with clear messages linked to story outcomes.
- Avoid mixing unrelated work in the same commit/story update.
- Do not include local scratch files (for example `.codex`) in commits.
