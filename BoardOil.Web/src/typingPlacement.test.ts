import { describe, expect, it } from 'vitest';
import cardSfc from './components/Card.vue?raw';
import dialogSfc from './components/CardEditorDialog.vue?raw';

function countOccurrences(content: string, fragment: string) {
  return content.split(fragment).length - 1;
}

describe('typing indicator placement', () => {
  it('renders card typing pill only in card title area', () => {
    expect(countOccurrences(cardSfc, 'class="typing-pill"')).toBe(1);
    expect(cardSfc.includes('typingSummary(card.id)')).toBe(true);
  });

  it('renders dialog typing pill only in card editor title slot', () => {
    expect(countOccurrences(dialogSfc, 'class="typing-pill"')).toBe(1);
    expect(dialogSfc.includes('v-if="isEditingCardTyping"')).toBe(true);
  });
});
