import { describe, expect, it } from 'vitest';
import editorSfc from './CardExternalUrlEditor.vue?raw';

function functionSection(start: string, end: string) {
  const startIndex = editorSfc.indexOf(start);
  const endIndex = editorSfc.indexOf(end, startIndex);
  expect(startIndex).toBeGreaterThan(-1);
  expect(endIndex).toBeGreaterThan(startIndex);
  return editorSfc.slice(startIndex, endIndex);
}

describe('CardExternalUrlEditor draft behaviour', () => {
  it('keeps input local until the edit is applied', () => {
    const inputSection = functionSection('function handleInput', 'function finishEdit');
    const applySection = functionSection('function finishEdit', 'function cancelEdit');

    expect(inputSection.includes('externalUrlModel.value =')).toBe(false);
    expect(applySection.includes('externalUrlModel.value = normalisedUrl;')).toBe(true);
  });

  it('does not update the card model when the edit is cancelled', () => {
    const cancelSection = functionSection('function cancelEdit', 'function syncInputValidity');

    expect(cancelSection.includes('externalUrlModel.value =')).toBe(false);
    expect(cancelSection.includes("draftUrl.value = externalUrlModel.value ?? '';")).toBe(true);
  });
});
