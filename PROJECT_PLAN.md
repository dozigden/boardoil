# Tag Support v1 (Cards + Global Tag Catalog)

## Summary
Implement first-class tags by adding card-level tag assignment/unassignment, a global editable tag list for style management, two style types (`solid`, `gradient`) with hex colors and `auto`/custom text color, and separate tag API endpoints. Card payloads include assigned tag names.

## Key Changes

### Data model
- Add `Tags` table with:
  - `Name` (display/canonical)
  - `NormalisedName` (unique, case-insensitive key)
  - `StyleName`
  - `StylePropertiesJson`
  - timestamps
- Add `CardTags` table with:
  - `CardId`
  - `TagName` (string only)
  - composite unique key (`CardId`, `TagName`)
  - max length 40
- Keep `CardTags.TagName` string-based (no FK to `Tags`) to preserve loose coupling.
- Add EF migration and update `BoardOilDbContext` mappings/indexes.

### Backend contracts and APIs
- Extend `CardDto` with `TagNames: IReadOnlyList<string>`.
- Extend `CreateCardRequest` and `UpdateCardRequest` with optional `TagNames` (replace semantics when provided).
- Add `TagDto` contract:
  - `Name`
  - `StyleName` (`solid` | `gradient`)
  - `StylePropertiesJson` (raw JSON string)
  - timestamps
- Add tag endpoints:
  - `GET /api/tags` (authenticated) for global tag list
  - `PATCH /api/tags/{name}` (card editor policy) for style-only updates
- Card create/update flow:
  - Parse/normalize input tags (trim, split commas, dedupe case-insensitive).
  - Enforce max 40 chars per tag.
  - Upsert missing tags into `Tags` with default style.
  - Persist card tag names in `CardTags`.
- Keep unused tags in catalog (no auto-delete).

### Services and validation
- Add `TagService`/`TagRepository` abstractions and validators for:
  - tag name rules (trimmed, non-empty, <= 40, spaces allowed)
  - style enum validity
  - hex color format (`#RRGGBB`)
  - style JSON schema checks by `StyleName`
- Add default style on tag auto-create:
  - `solid`
  - random `backgroundColor` hex
  - `textColorMode: "auto"`
- Update board/card retrieval pipelines so board payload cards include `TagNames`.

### Frontend
- Extend board types with:
  - `Card.tagNames: string[]`
  - `Tag` model with `name`, `styleName`, `stylePropertiesJson`
- Update board store + API client:
  - save card now sends full `tagNames`
  - add tag API calls (`getTags`, `updateTagStyle`) and tag store state
- Card rendering:
  - show readonly tag pills on board cards
- Card editor:
  - show assigned tags as pills with remove `x`
  - input supports free text and comma-separated tags
  - `Enter` assigns parsed tags, clears input, keeps focus
- Add Tag List management view (route + header menu entry, authenticated for all users):
  - list tags
  - edit style only (solid/gradient + color fields + auto/custom text)
  - persist style via patch endpoint
- Style rendering:
  - solid uses background + text color mode
  - gradient uses left/right blend
  - auto text picks black/white via luminance contrast helper

## Test Plan

### Services/API (.NET)
- Tag validation: length, trimming, commas, dedupe, hex validation, style schema validation.
- Card create/update: assign tags, replace tags, unassign, auto-create missing tag entries.
- Board payload includes card tags.
- Tag endpoints: list + style update authorization and validation.

### EF/integration
- Migration applies cleanly; unique constraints for normalised tag names and card-tag duplicates.
- End-to-end: create card with new tags => tags exist in catalog with defaults.

### Frontend (Vitest + checks)
- Tag parsing utility behavior (`enter`, commas, trimming, dedupe).
- Card editor interactions (add/remove tags, focus retention, payload sent).
- Card component pill rendering.
- Tag manager style edit flow and API integration.
- Run `npm run check` in `BoardOil.Web`.

## Assumptions / Defaults
- Tag uniqueness is case-insensitive + trim (`"Bug"` == `" bug "`), with canonical display name from first creation.
- Card tag updates use replace semantics when `tagNames` is provided.
- Unused tags are retained in global catalog.
- Hex colors accepted as 6-digit HTML hex (`#RRGGBB`) for v1.
- Tag style management is style-only in v1 (no rename/delete UI).
