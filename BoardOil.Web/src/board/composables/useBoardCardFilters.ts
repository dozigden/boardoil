import { computed, ref, type Ref } from 'vue';
import type { Board, BoardColumn, CardType, Slick, Tag } from '../../shared/types/boardTypes';
import type { TagFilterState, TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import { createCardSearchAndTagMatcher, type CardSearchAndTagFilter } from '../utils/cardFilters';

export function useBoardCardFilters(
  board: Ref<Board | null>,
  tags: Ref<Tag[]>,
  slicks: Ref<Slick[]>,
  cardTypes: Ref<CardType[]>
) {
  const cardSearchText = ref('');
  const tagFilterStates = ref<TagFilterStateMap>({});
  const slickFilterStates = ref<TagFilterStateMap>({});
  const cardTypeFilterStates = ref<TagFilterStateMap>({});
  const isTagFilterMenuOpen = ref(false);

  const availableTagNames = computed(() =>
    tags.value
      .map(tag => tag.name)
      .sort((left, right) => left.localeCompare(right))
  );

  const availableSlickNames = computed(() =>
    slicks.value
      .map(slick => slick.name)
      .sort((left, right) => left.localeCompare(right))
  );

  const availableCardTypes = computed(() =>
    [...cardTypes.value].sort((left, right) => left.name.localeCompare(right.name))
  );

  const includedTagNames = computed(() =>
    availableTagNames.value.filter(tagName => resolveTagFilterState(tagFilterStates.value, tagName) === 'include')
  );

  const excludedTagNames = computed(() =>
    availableTagNames.value.filter(tagName => resolveTagFilterState(tagFilterStates.value, tagName) === 'exclude')
  );

  const includedSlickNames = computed(() =>
    availableSlickNames.value.filter(slickName => resolveTagFilterState(slickFilterStates.value, slickName) === 'include')
  );

  const excludedSlickNames = computed(() =>
    availableSlickNames.value.filter(slickName => resolveTagFilterState(slickFilterStates.value, slickName) === 'exclude')
  );

  const includedCardTypeIds = computed(() =>
    availableCardTypes.value
      .filter(cardType => resolveTagFilterState(cardTypeFilterStates.value, String(cardType.id)) === 'include')
      .map(cardType => cardType.id)
  );

  const excludedCardTypeIds = computed(() =>
    availableCardTypes.value
      .filter(cardType => resolveTagFilterState(cardTypeFilterStates.value, String(cardType.id)) === 'exclude')
      .map(cardType => cardType.id)
  );

  const cardFilters = computed<CardSearchAndTagFilter>(() => ({
    searchText: cardSearchText.value,
    includedTagNames: [...includedTagNames.value],
    excludedTagNames: [...excludedTagNames.value],
    includedSlickNames: [...includedSlickNames.value],
    excludedSlickNames: [...excludedSlickNames.value],
    includedCardTypeIds: [...includedCardTypeIds.value],
    excludedCardTypeIds: [...excludedCardTypeIds.value]
  }));

  const filteredColumns = computed<BoardColumn[]>(() => {
    if (!board.value) {
      return [];
    }

    const matcher = createCardSearchAndTagMatcher(cardFilters.value);
    return board.value.columns.map(column => ({
      ...column,
      cards: column.cards.filter(card => matcher(card))
    }));
  });

  const hasActiveCardFilters = computed(() =>
    cardSearchText.value.trim().length > 0
    || includedTagNames.value.length > 0
    || excludedTagNames.value.length > 0
    || includedSlickNames.value.length > 0
    || excludedSlickNames.value.length > 0
    || includedCardTypeIds.value.length > 0
    || excludedCardTypeIds.value.length > 0
  );

  function clearCardFilters() {
    cardSearchText.value = '';
    tagFilterStates.value = {};
    slickFilterStates.value = {};
    cardTypeFilterStates.value = {};
    isTagFilterMenuOpen.value = false;
  }

  return {
    cardSearchText,
    tagFilterStates,
    slickFilterStates,
    cardTypeFilterStates,
    isTagFilterMenuOpen,
    availableTagNames,
    availableCardTypes,
    includedTagNames,
    excludedTagNames,
    includedSlickNames,
    excludedSlickNames,
    includedCardTypeIds,
    excludedCardTypeIds,
    cardFilters,
    filteredColumns,
    hasActiveCardFilters,
    clearCardFilters
  };
}

function resolveTagFilterState(filterStates: TagFilterStateMap, tagName: string): TagFilterState {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return 'none';
  }

  return filterStates[normalisedTagName] ?? 'none';
}

function normaliseTagName(tagName: string) {
  return tagName.trim().toLocaleLowerCase();
}
