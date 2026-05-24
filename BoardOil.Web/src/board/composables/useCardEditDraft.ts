import { ref } from 'vue';
import type { Card, CardEditModel } from '../../shared/types/boardTypes';
import { createEditModelFromCard } from '../mappers/cardEditModel';

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
  const cardDraft = ref<CardEditModel | null>(null);
  const cardDraftId = ref<number | null>(null);
  const isDirty = ref(false);

  function clearDraft() {
    cardDraft.value = null;
    cardDraftId.value = null;
    isDirty.value = false;
  }

  function initializeDraftFromCard(card: Card) {
    if (cardDraftId.value === card.id) {
      return false;
    }

    const nextDraft = createEditModelFromCard(card);
    cardDraft.value = cloneCardEditModel(nextDraft);
    cardDraftId.value = card.id;
    isDirty.value = false;
    return true;
  }

  function patchDraft(update: Partial<CardEditModel>, markDirty = false) {
    if (cardDraft.value === null) {
      return;
    }

    const previousDraft = cardDraft.value;
    cardDraft.value = {
      ...previousDraft,
      ...update
    };

    if (markDirty && !areCardEditModelsEqual(cardDraft.value, previousDraft)) {
      isDirty.value = true;
    }
  }

  return {
    cardDraft,
    cardDraftId,
    isDirty,
    clearDraft,
    initializeDraftFromCard,
    patchDraft
  };
}
