import type { Card } from '../types/boardTypes';

export type CardSearchAndTagFilter = {
  searchText: string;
  includedTagNames: string[];
  excludedTagNames: string[];
};

type FilterableCard = Pick<Card, 'title' | 'description' | 'tagNames'>;

export function createCardSearchAndTagMatcher(filter: CardSearchAndTagFilter) {
  const normalisedSearchText = normaliseSearchText(filter.searchText);
  const includedTagNames = normaliseTagNameSet(filter.includedTagNames);
  const excludedTagNames = normaliseTagNameSet(filter.excludedTagNames);
  const hasIncludeFilter = includedTagNames.size > 0;
  const hasExcludeFilter = excludedTagNames.size > 0;

  return (card: FilterableCard) => {
    if (normalisedSearchText.length > 0) {
      const searchableText = `${card.title} ${card.description}`.toLocaleLowerCase();
      if (!searchableText.includes(normalisedSearchText)) {
        return false;
      }
    }

    if (!hasIncludeFilter && !hasExcludeFilter) {
      return true;
    }

    const cardTagNames = normaliseTagNameSet(card.tagNames);

    if (hasIncludeFilter && !hasAnyTag(cardTagNames, includedTagNames)) {
      return false;
    }

    if (hasExcludeFilter && hasAnyTag(cardTagNames, excludedTagNames)) {
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
