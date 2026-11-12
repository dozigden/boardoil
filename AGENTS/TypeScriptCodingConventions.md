# TypeScript Coding Conventions

Use these conventions for TypeScript throughout the repository, including Vue `<script>` blocks, browser tests and tooling.

## Naming and Language

- Prefer British English spellings unless integrating with an external API that requires a specific spelling.
- Keep function and variable names aligned with their behaviour and meaning.

## Comments

- Prefer self-explanatory code. Comment only to explain non-obvious reasons or constraints, not what the code does.
- Default to no comment. Improve naming or structure before adding an explanation.
- Keep investigation history, validation results and change summaries in the board story or review description. Retain only essential rationale beside the code.

## Function Design

- Do not use nested ternary expressions. Prefer explicit `if`/`switch` branches or small helper functions.

Follow [Frontend guidance](Frontend.md) for frontend behaviour and contracts, [Pinia guidance](frontend-pinia.md) for stores and realtime integration, and [Testing guidance](Testing.md) for test design.
