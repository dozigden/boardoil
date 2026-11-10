export type BulkEditSlickOperation = 'none' | 'clear' | 'set';

export type BulkEditCardsOptions = {
  moveTargetColumnId?: number | null;
  moveTargetCardId?: number | null;
  addTagNames?: string[];
  removeTagNames?: string[];
  slickName?: string | null;
};
