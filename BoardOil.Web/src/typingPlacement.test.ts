import { describe, expect, it } from 'vitest';
import cardSfc from './board/components/Card.vue?raw';
import dialogSfc from './board/components/CardEditorDialog.vue?raw';

function countOccurrences(content: string, fragment: string) {
  return content.split(fragment).length - 1;
}

describe('typing indicator placement', () => {
  it('does not render typing pill in card template', () => {
    expect(countOccurrences(cardSfc, 'class="typing-pill"')).toBe(0);
    expect(cardSfc.includes('typingSummary(card.id)')).toBe(false);
  });

  it('does not render typing pill in card editor dialog template', () => {
    expect(countOccurrences(dialogSfc, 'class="typing-pill"')).toBe(0);
    expect(dialogSfc.includes('isEditingCardTyping')).toBe(false);
  });
});
