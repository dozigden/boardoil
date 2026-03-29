export function parseTagInputValues(values: string[]): string[] {
  const parsed: string[] = [];
  const seen = new Set<string>();

  for (const value of values) {
    for (const part of value.split(',')) {
      const tag = part.trim();
      if (!tag) {
        continue;
      }

      const normalized = normalizeTagName(tag);
      if (seen.has(normalized)) {
        continue;
      }

      seen.add(normalized);
      parsed.push(tag);
    }
  }

  return parsed;
}

export function mergeTagNames(existing: string[], additions: string[]): string[] {
  const merged: string[] = [];
  const seen = new Set<string>();

  for (const tag of [...existing, ...additions]) {
    const trimmed = tag.trim();
    if (!trimmed) {
      continue;
    }

    const normalized = normalizeTagName(trimmed);
    if (seen.has(normalized)) {
      continue;
    }

    seen.add(normalized);
    merged.push(trimmed);
  }

  return merged;
}

export function getTagCompletionQuery(tagEntry: string): string {
  const lastCommaIndex = tagEntry.lastIndexOf(',');
  const rawQuery = lastCommaIndex < 0
    ? tagEntry
    : tagEntry.slice(lastCommaIndex + 1);

  return rawQuery.trim();
}

export function getTagCompletionSuggestions(
  availableTagNames: string[],
  tagEntry: string,
  selectedTagNames: string[],
  limit = 8
): string[] {
  const query = getTagCompletionQuery(tagEntry);
  if (!query) {
    return [];
  }

  const normalisedQuery = normalizeTagName(query);
  const selected = new Set(selectedTagNames.map(tagName => normalizeTagName(tagName)));
  const seen = new Set<string>();
  const suggestions: string[] = [];

  for (const tagName of availableTagNames) {
    const trimmed = tagName.trim();
    if (!trimmed) {
      continue;
    }

    const normalised = normalizeTagName(trimmed);
    if (seen.has(normalised) || selected.has(normalised)) {
      continue;
    }

    if (!normalised.startsWith(normalisedQuery)) {
      continue;
    }

    seen.add(normalised);
    suggestions.push(trimmed);
    if (suggestions.length >= limit) {
      break;
    }
  }

  return suggestions;
}

function normalizeTagName(tagName: string) {
  return tagName.trim().toLowerCase();
}
