const BARE_TASK_ITEM_PATTERN = /^(\s*)\[( |x|X)\](.*)$/;
const LIST_TASK_ITEM_PATTERN = /^(\s*)[-*+]\s+\[( |x|X)\](.*)$/;

export function normaliseMarkdown(value: string, maxLength?: number): string {
  const withTaskListSyntax = normaliseBareTaskItems(value);

  if (!Number.isFinite(maxLength) || (maxLength ?? 0) <= 0) {
    return withTaskListSyntax;
  }

  return withTaskListSyntax.slice(0, maxLength);
}

function normaliseBareTaskItems(value: string): string {
  return value
    .split('\n')
    .map(line => {
      if (LIST_TASK_ITEM_PATTERN.test(line)) {
        return line;
      }

      const match = line.match(BARE_TASK_ITEM_PATTERN);
      if (!match) {
        return line;
      }

      const [, indentation, state, trailing] = match;
      return `${indentation}- [${state}]${trailing}`;
    })
    .join('\n');
}
