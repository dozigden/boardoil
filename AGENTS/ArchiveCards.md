# Archive Cards Guidance

This file defines archive snapshot, search, and payload versioning conventions for story archive flows.

## Scope

- Applies to archive persistence for board cards.
- Archive storage should be flexible for future card-shape changes.
- Avoid denormalised archive schemas that duplicate most live card columns.

## Archive Storage Model

- Use a lightweight archive envelope model with stable metadata columns plus a JSON snapshot payload.
- Keep the JSON payload as the source of truth for archived card data.
- Keep searchable metadata in dedicated columns for efficient list/filter operations.

Recommended metadata fields:
- `BoardId`
- `OriginalCardId`
- `ArchivedAtUtc`
- `SnapshotJson`
- `SearchTitle`
- `SearchTagsJson`

Notes:
- `SearchTagsJson` should store a JSON array of tag names.
- Avoid CSV for tags because tag names may contain commas.

## Search Rules

- Archive search is substring (`contains`) and case-insensitive.
- Search must match when the search term appears in either:
  - `SearchTitle`, or
  - any tag in `SearchTagsJson`.

SQLite guidance:
- Title: `LIKE` with `COLLATE NOCASE`.
- Tags JSON: use `json_each(SearchTagsJson)` and `EXISTS` for tag substring matches.

Example pattern:
- `SearchTitle LIKE '%' || term || '%' COLLATE NOCASE`
- `EXISTS (SELECT 1 FROM json_each(SearchTagsJson) t WHERE t.value LIKE '%' || term || '%' COLLATE NOCASE)`

## Snapshot Payload Envelope and Versioning

- `SnapshotJson` must use a versioned envelope.

Envelope shape:

```json
{
  "schema": "archived-card",
  "version": 1,
  "capturedAtUtc": "2026-04-19T16:00:00Z",
  "payload": {
    "...": "archived card snapshot fields"
  }
}
```

Rules:
- Always write the latest envelope version.
- For additive, backward-compatible payload fields, keep the current version.
- For breaking payload changes (rename/remove/type change), bump `version`.
- On read, parse envelope first and run upgrade chain steps (`v1 -> v2 -> ... -> current`) when needed.
- If a stored snapshot has an unknown newer version, do not fail list/search flows; treat payload as opaque and continue using archive metadata.

## Compatibility Intent

- Archive list/search should not depend on fully deserialising `SnapshotJson`.
- Keep metadata-based list/search resilient even when payload versions differ.
- Preserve room for future restore support without forcing table redesign for each card contract change.

## Testing Pattern for Version Compatibility

- Treat archive compatibility tests as **reader tests**, not writer tests.
- For versioned restore tests (for example `*Unarchive*V1Tests`):
  - seed `EntityArchivedCard` rows directly
  - provide explicit snapshot JSON for the target version (`v1`, `v2`, etc.)
  - do **not** create test setup snapshots by calling `ArchiveCardAsync`, because that always writes the current/latest version.
- Keep versioned suites frozen once added:
  - `V1` tests should keep asserting `V1` payload behavior even after current writer moves to newer versions.
  - Add new suites (`V2`, `V3`, ...) for new envelope versions rather than rewriting older suites.
- Keep writer tests separate:
  - add/maintain dedicated tests that assert `ArchiveCardAsync` writes `CurrentVersion`.
  - keep these tests out of versioned compatibility suites.
