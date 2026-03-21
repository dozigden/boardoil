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

function normalizeTagName(tagName: string) {
  return tagName.trim().toLowerCase();
}
