import type { Card, CardEditModel } from '../../shared/types/boardTypes';
import { createEditModelFromCard } from '../mappers/cardEditModel';
import { useEntityEditDraft } from '../../shared/composables/useEntityEditDraft';

function areStringArraysEqual(left: string[], right: string[]) {
  if (left.length !== right.length) {
    return false;
  }

  for (let index = 0; index < left.length; index += 1) {
    if (left[index] !== right[index]) {
      return false;
    }
  }

  return true;
}

function areCardEditModelsEqual(left: CardEditModel, right: CardEditModel) {
  return left.title === right.title
    && left.description === right.description
    && areStringArraysEqual(left.tagNames, right.tagNames)
    && left.cardTypeId === right.cardTypeId
    && left.boardColumnId === right.boardColumnId
    && left.assignedUserId === right.assignedUserId
    && left.slickName === right.slickName;
}

function cloneCardEditModel(model: CardEditModel): CardEditModel {
  return {
    ...model,
    tagNames: [...model.tagNames]
  };
}

export function useCardEditDraft() {
  const {
    draft,
    sourceId,
    isDirty,
    initFromSource,
    patchFromUser,
    patchFromSystem,
    clear
  } = useEntityEditDraft<Card, CardEditModel, number>({
    getId: card => card.id,
    toDraft: createEditModelFromCard,
    cloneDraft: cloneCardEditModel,
    areEqual: areCardEditModelsEqual
  });

  return {
    cardDraft: draft,
    cardDraftId: sourceId,
    isDirty,
    clearDraft: clear,
    initializeDraftFromCard: initFromSource,
    patchFromUser,
    patchFromSystem
  };
}
