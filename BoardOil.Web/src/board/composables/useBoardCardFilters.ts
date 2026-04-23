import { computed, ref, type Ref } from 'vue';
import type { Board, BoardColumn, Tag } from '../../shared/types/boardTypes';
import type { TagFilterState, TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import { createCardSearchAndTagMatcher, type CardSearchAndTagFilter } from '../utils/cardFilters';

export function useBoardCardFilters(board: Ref<Board | null>, tags: Ref<Tag[]>) {
  const cardSearchText = ref('');
  const tagFilterStates = ref<TagFilterStateMap>({});
  const isTagFilterMenuOpen = ref(false);

  const availableTagNames = computed(() =>
    tags.value
      .map(tag => tag.name)
      .sort((left, right) => left.localeCompare(right))
  );

  const includedTagNames = computed(() =>
    availableTagNames.value.filter(tagName => resolveTagFilterState(tagFilterStates.value, tagName) === 'include')
  );

  const excludedTagNames = computed(() =>
    availableTagNames.value.filter(tagName => resolveTagFilterState(tagFilterStates.value, tagName) === 'exclude')
  );

  const cardFilters = computed<CardSearchAndTagFilter>(() => ({
    searchText: cardSearchText.value,
    includedTagNames: [...includedTagNames.value],
    excludedTagNames: [...excludedTagNames.value]
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
  );

  function clearCardFilters() {
    cardSearchText.value = '';
    tagFilterStates.value = {};
    isTagFilterMenuOpen.value = false;
  }

  return {
    cardSearchText,
    tagFilterStates,
    isTagFilterMenuOpen,
    availableTagNames,
    includedTagNames,
    excludedTagNames,
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
