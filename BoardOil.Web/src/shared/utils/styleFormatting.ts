export function normaliseEmojiForRender(rawEmoji: string | null | undefined): string | null {
  const trimmed = rawEmoji?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}
