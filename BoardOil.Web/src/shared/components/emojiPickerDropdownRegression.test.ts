import { describe, expect, it } from 'vitest';
import emojiPickerDropdownSfc from './EmojiPickerDropdown.vue?raw';
import { normaliseTagEmojiForRender } from '../utils/tagStyles';

describe('emoji picker dropdown regressions', () => {
  it('keeps an existing value visible even when it is not supplied by the picker', () => {
    expect(normaliseTagEmojiForRender('  legacy-value  ')).toBe('legacy-value');
    expect(emojiPickerDropdownSfc.includes('<span v-if="selectedEmoji" class="bo-emoji">{{ selectedEmoji }}</span>')).toBe(true);
    expect(emojiPickerDropdownSfc.includes(':disabled="disabled || !selectedEmoji"')).toBe(true);
  });

  it('allows an unsupported existing value to be cleared or replaced', () => {
    expect(emojiPickerDropdownSfc.includes("emit('update:modelValue', null);")).toBe(true);
    expect(emojiPickerDropdownSfc.includes("emit('update:modelValue', emoji);")).toBe(true);
  });
});
