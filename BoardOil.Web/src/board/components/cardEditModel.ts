import type { Card, CardEditModel } from '../../shared/types/boardTypes';

export function createCardEditModel(card: Card): CardEditModel {
  return {
    customFieldValues: { ...card.customFieldValues },
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
    ...(model.customFieldValues === undefined ? {} : { customFieldValues: { ...model.customFieldValues } }),
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

  if (left.customFieldValues === undefined || right.customFieldValues === undefined) {
    if (left.customFieldValues !== right.customFieldValues) return false;
  } else if (Object.keys(left.customFieldValues).length !== Object.keys(right.customFieldValues).length
    || Object.entries(left.customFieldValues).some(([code, value]) => right.customFieldValues![code] !== value)) {
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
