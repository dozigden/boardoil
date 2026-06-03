import { describe, expect, it } from 'vitest';
import cardEditorDialogSfc from './CardEditorDialog.vue?raw';
import cardTypeEditorDialogSfc from './CardTypeEditorDialog.vue?raw';
import slickEditorDialogSfc from './SlickEditorDialog.vue?raw';
import tagEditorDialogSfc from './TagEditorDialog.vue?raw';

describe('routed dialog loading regressions', () => {
  it('does not watch lookup catalogues while loading card editor draft data', () => {
    expect(cardEditorDialogSfc.includes('[boardId, routeCardId, editingCard, board]')).toBe(true);
    expect(cardEditorDialogSfc.includes('[boardId, routeCardId, editingCard, board, cardTypes, boardMembers, slicks]')).toBe(false);
  });

  it('initializes card editor draft before loading supporting lookup catalogues', () => {
    const draftIndex = cardEditorDialogSfc.indexOf('initializeDraftForCard(nextCard);');
    const lookupIndex = cardEditorDialogSfc.indexOf('const lookupsLoaded = await ensureEditorLookupsLoaded');
    expect(draftIndex).toBeGreaterThan(-1);
    expect(lookupIndex).toBeGreaterThan(-1);
    expect(draftIndex).toBeLessThan(lookupIndex);
  });

  it('treats empty loaded routed-dialog catalogues as loaded', () => {
    expect(cardEditorDialogSfc.includes('cardTypes.value.length === 0')).toBe(false);
    expect(cardEditorDialogSfc.includes('boardMembers.value.length === 0')).toBe(false);
    expect(cardEditorDialogSfc.includes('slicks.value.length === 0')).toBe(false);
    expect(slickEditorDialogSfc.includes('slickStore.slicks.length === 0')).toBe(false);
    expect(tagEditorDialogSfc.includes('tagStore.tags.length === 0')).toBe(false);
  });

  it('keeps card type dialog free of empty-list reload checks', () => {
    expect(cardTypeEditorDialogSfc.includes('cardTypes.value.length === 0')).toBe(false);
  });
});
