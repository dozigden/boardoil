import { describe, expect, it } from 'vitest';
import dialogSfc from './CardEditorDialog.vue?raw';

function countOccurrences(content: string, fragment: string) {
  return content.split(fragment).length - 1;
}

describe('CardEditorDialog unsaved-change guard wiring', () => {
  it('keeps description and comment dirty tracking focus-gated', () => {
    expect(dialogSfc.includes('function applyUserDescriptionEdit(value: string) {')).toBe(true);
    expect(dialogSfc.includes('function syncDescriptionFromEditor(value: string) {')).toBe(true);
    expect(dialogSfc.includes('if (descriptionEditorFocused.value) {')).toBe(true);
    expect(dialogSfc.includes('applyUserDescriptionEdit(value);')).toBe(true);
    expect(dialogSfc.includes('syncDescriptionFromEditor(value);')).toBe(true);
    expect(dialogSfc.includes('if (commentEditorFocused.value && newCommentText.value !== value) {')).toBe(true);
  });

  it('keeps editor focus/blur hooks connected in template', () => {
    expect(countOccurrences(dialogSfc, '@focus="handleDescriptionEditorFocus"')).toBe(1);
    expect(countOccurrences(dialogSfc, '@blur="handleDescriptionEditorBlur"')).toBe(1);
    expect(countOccurrences(dialogSfc, '@focus="handleCommentEditorFocus"')).toBe(1);
    expect(countOccurrences(dialogSfc, '@blur="handleCommentEditorBlur"')).toBe(1);
  });
});
