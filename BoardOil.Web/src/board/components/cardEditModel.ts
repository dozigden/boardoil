import type { Card, CardEditModel } from '../../shared/types/boardTypes';

export function createCardEditModel(card: Card): CardEditModel {
  return {
    fields: { ...card.fields },
    title: card.title,
    description: card.description,
    externalUrl: card.externalUrl,
    tagNames: [...card.tagNames],
    cardTypeId: card.cardTypeId,
    boardColumnId: card.boardColumnId,
    assignedUserId: card.assignedUserId ?? null,
    slickName: card.slickName ?? null
  };
}

export function cloneCardEditModel(model: CardEditModel): CardEditModel {
  return {
    ...model,
    ...(model.fields === undefined ? {} : { fields: { ...model.fields } }),
    tagNames: [...model.tagNames]
  };
}

export function areCardEditModelsEqual(left: CardEditModel, right: CardEditModel) {
  if (left.title !== right.title
    || left.description !== right.description
    || left.externalUrl !== right.externalUrl) {
    return false;
  }

  if (left.cardTypeId !== right.cardTypeId || left.boardColumnId !== right.boardColumnId) {
    return false;
  }

  if (left.assignedUserId !== right.assignedUserId || left.slickName !== right.slickName) {
    return false;
  }

  if (left.fields === undefined || right.fields === undefined) {
    if (left.fields !== right.fields) return false;
  } else if (Object.keys(left.fields).length !== Object.keys(right.fields).length
    || Object.entries(left.fields).some(([code, value]) => right.fields![code] !== value)) {
    return false;
  }
  return areStringArraysEqual(left.tagNames, right.tagNames);
}

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
