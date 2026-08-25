import { describe, expect, it } from 'vitest';
import cardEditorDialogSfc from './CardEditorDialog.vue?raw';
import archivedCardDetailSfc from './ArchivedCardDetailContent.vue?raw';
import boardTypesSource from '../../shared/types/boardTypes.ts?raw';

describe('logical card timestamp presentation', () => {
  it('uses logical timestamp fields in active and archived card details', () => {
    expect(boardTypesSource).toContain('cardCreatedUtc: string;');
    expect(boardTypesSource).toContain('cardUpdatedUtc: string;');
    expect(cardEditorDialogSfc).toContain('editingCard!.cardCreatedUtc');
    expect(cardEditorDialogSfc).toContain('editingCard!.cardUpdatedUtc');
    expect(archivedCardDetailSfc).toContain('card.cardCreatedUtc');
    expect(archivedCardDetailSfc).toContain('card.cardUpdatedUtc');
  });
});
