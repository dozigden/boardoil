import type { Card } from '../../shared/types/boardTypes';

export type CardSearchAndTagFilter = {
  searchText: string;
  includedTagNames: string[];
  excludedTagNames: string[];
  includedSlickNames: string[];
  excludedSlickNames: string[];
  includedCardTypeIds: number[];
  excludedCardTypeIds: number[];
};

type FilterableCard = Pick<Card, 'title' | 'description' | 'tagNames' | 'slickName' | 'cardTypeId'>;

export function createCardSearchAndTagMatcher(filter: CardSearchAndTagFilter) {
  const normalisedSearchText = normaliseSearchText(filter.searchText);
  const includedTagNames = normaliseTagNameSet(filter.includedTagNames);
  const excludedTagNames = normaliseTagNameSet(filter.excludedTagNames);
  const includedSlickNames = normaliseTagNameSet(filter.includedSlickNames);
  const excludedSlickNames = normaliseTagNameSet(filter.excludedSlickNames);
  const includedCardTypeIds = normaliseIdSet(filter.includedCardTypeIds);
  const excludedCardTypeIds = normaliseIdSet(filter.excludedCardTypeIds);
  const hasIncludeFilter = includedTagNames.size > 0;
  const hasExcludeFilter = excludedTagNames.size > 0;
  const hasIncludeSlickFilter = includedSlickNames.size > 0;
  const hasExcludeSlickFilter = excludedSlickNames.size > 0;
  const hasIncludeCardTypeFilter = includedCardTypeIds.size > 0;
  const hasExcludeCardTypeFilter = excludedCardTypeIds.size > 0;

  return (card: FilterableCard) => {
    if (normalisedSearchText.length > 0) {
      const searchableText = `${card.title} ${card.description}`.toLocaleLowerCase();
      if (!searchableText.includes(normalisedSearchText)) {
        return false;
      }
    }

    if (!hasIncludeFilter
      && !hasExcludeFilter
      && !hasIncludeSlickFilter
      && !hasExcludeSlickFilter
      && !hasIncludeCardTypeFilter
      && !hasExcludeCardTypeFilter) {
      return true;
    }

    const cardTagNames = normaliseTagNameSet(card.tagNames);
    const cardSlickNames = normaliseOptionalName(card.slickName);

    if (hasIncludeFilter && !hasAnyTag(cardTagNames, includedTagNames)) {
      return false;
    }

    if (hasExcludeFilter && hasAnyTag(cardTagNames, excludedTagNames)) {
      return false;
    }

    if (hasIncludeSlickFilter && !hasAnyTag(cardSlickNames, includedSlickNames)) {
      return false;
    }

    if (hasExcludeSlickFilter && hasAnyTag(cardSlickNames, excludedSlickNames)) {
      return false;
    }

    if (hasIncludeCardTypeFilter && !includedCardTypeIds.has(card.cardTypeId)) {
      return false;
    }

    if (hasExcludeCardTypeFilter && excludedCardTypeIds.has(card.cardTypeId)) {
      return false;
    }

    return true;
  };
}

function hasAnyTag(left: Set<string>, right: Set<string>) {
  for (const tagName of left) {
    if (right.has(tagName)) {
      return true;
    }
  }

  return false;
}

function normaliseSearchText(value: string) {
  return value.trim().toLocaleLowerCase();
}

function normaliseTagNameSet(tagNames: string[]) {
  const normalised = new Set<string>();
  for (const tagName of tagNames) {
    const normalisedTagName = tagName.trim().toLocaleLowerCase();
    if (!normalisedTagName) {
      continue;
    }

    normalised.add(normalisedTagName);
  }

  return normalised;
}

function normaliseOptionalName(value: string | null | undefined) {
  if (typeof value !== 'string') {
    return new Set<string>();
  }

  return normaliseTagNameSet([value]);
}

function normaliseIdSet(ids: number[]) {
  const normalised = new Set<number>();
  for (const id of ids) {
    if (!Number.isFinite(id)) {
      continue;
    }

    normalised.add(Math.trunc(id));
  }

  return normalised;
}
