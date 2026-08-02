import { describe, expect, it } from 'vitest';
import editorSfc from './MdEditor.vue?raw';

function sectionBetween(start: string, end: string) {
  const startIndex = editorSfc.indexOf(start);
  const endIndex = editorSfc.indexOf(end, startIndex);
  expect(startIndex).toBeGreaterThan(-1);
  expect(endIndex).toBeGreaterThan(startIndex);
  return editorSfc.slice(startIndex, endIndex);
}

describe('MdEditor plain-text synchronisation', () => {
  it('does not parse model updates into the hidden rich editor while plain-text mode is active', () => {
    const modelWatch = sectionBetween('watch(\n  normalisedModelValue,', 'watch(\n  isPlainTextMode,');
    const plainTextGuardIndex = modelWatch.indexOf('if (isPlainTextMode.value) {');
    const returnIndex = modelWatch.indexOf('return;', plainTextGuardIndex);
    const richEditorSyncIndex = modelWatch.indexOf('setEditorContent(nextValue);');

    expect(plainTextGuardIndex).toBeGreaterThan(-1);
    expect(returnIndex).toBeGreaterThan(plainTextGuardIndex);
    expect(richEditorSyncIndex).toBeGreaterThan(returnIndex);
  });

  it('does not emit updates when rich-editor content is synchronised programmatically', () => {
    const setContentFunction = sectionBetween(
      'function setEditorContent(value: string) {',
      'watch(\n  normalisedModelValue,'
    );

    expect(setContentFunction.includes('emitUpdate: false')).toBe(true);
  });

  it('synchronises the completed draft when switching back to rich mode', () => {
    const toggleFunction = sectionBetween(
      'function togglePlainTextMode() {',
      'function onPlainTextInput(value: string) {'
    );
    const disablePlainTextIndex = toggleFunction.indexOf('isPlainTextMode.value = false;');
    const richEditorSyncIndex = toggleFunction.indexOf('setEditorContent(nextValue);');

    expect(disablePlainTextIndex).toBeGreaterThan(-1);
    expect(richEditorSyncIndex).toBeGreaterThan(disablePlainTextIndex);
  });
});
