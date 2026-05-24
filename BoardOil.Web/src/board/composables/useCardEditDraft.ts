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
  const baselineDraft = ref<CardEditModel | null>(null);
  const isDirty = ref(false);

  function clearDraft() {
    cardDraft.value = null;
    cardDraftId.value = null;
    baselineDraft.value = null;
    isDirty.value = false;
  }

  function initializeDraftFromCard(card: Card) {
    if (cardDraftId.value === card.id) {
      return false;
    }

    const nextDraft = createEditModelFromCard(card);
    cardDraft.value = cloneCardEditModel(nextDraft);
    baselineDraft.value = cloneCardEditModel(nextDraft);
    cardDraftId.value = card.id;
    isDirty.value = false;
    return true;
  }

  function patchFromSystem(update: Partial<CardEditModel>) {
    if (cardDraft.value === null) {
      return;
    }

    cardDraft.value = {
      ...cardDraft.value,
      ...update
    };
  }

  function patchFromUser(update: Partial<CardEditModel>) {
    if (cardDraft.value === null) {
      return;
    }

    const previousDraft = cardDraft.value;
    const nextDraft = {
      ...previousDraft,
      ...update
    };
    cardDraft.value = nextDraft;

    if (!isDirty.value && !areCardEditModelsEqual(nextDraft, previousDraft)) {
      isDirty.value = true;
    }
  }

  return {
    cardDraft,
    cardDraftId,
    isDirty,
    clearDraft,
    initializeDraftFromCard,
    patchFromUser,
    patchFromSystem
  };
}
