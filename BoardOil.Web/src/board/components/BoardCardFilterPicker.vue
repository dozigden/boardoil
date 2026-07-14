<template>
  <div ref="dropdownRoot" class="board-card-filter-dropdown">
    <div class="board-card-filter-trigger-row">
      <div class="board-card-filter-button-wrap">
        <button
          type="button"
          class="btn btn--secondary board-card-filter-toggle"
          :class="{ 'board-card-filter-toggle--active': hasActiveFilters }"
          aria-label="Card filters"
          title="Card filters"
          :aria-controls="menuId"
          :aria-expanded="open"
          @click="emit('update:open', !open)"
        >
          <Filter :size="14" aria-hidden="true" />
          <span class="board-card-filter-toggle-label">Filter</span>
        </button>

        <section
          v-if="open"
          ref="menuRef"
          :id="menuId"
          class="panel panel--compact board-card-filter-menu"
          :style="menuStyle"
          aria-label="Card filter matrix"
        >
          <div class="board-card-filter-menu-content">
            <section
              v-if="availableTagNames.length > 0"
              class="board-card-filter-section"
            >
              <TagTriStateMatrix
                :available-tag-names="availableTagNames"
                :states="tagFilterStates"
                :labels="{ left: 'Exclude', middle: 'Tag', right: 'Include' }"
                :ariaLabel="'Tag filter matrix'"
                left-action-prefix="Move to exclude"
                middle-action-prefix="Move to tag"
                right-action-prefix="Move to include"
                :show-directional-cursor="true"
                :enable-bounce="true"
                scroll-mode="parent"
                @update:states="emit('update:tagFilterStates', $event)"
              />
            </section>

            <section
              v-if="availableSlickNames.length > 0"
              class="board-card-filter-section"
            >
              <TagTriStateMatrix
                :available-tag-names="availableSlickNames"
                :states="slickFilterStates"
                :labels="{ left: 'Exclude', middle: 'Slick', right: 'Include' }"
                :ariaLabel="'Slick filter matrix'"
                semantic-scope="slick"
                left-action-prefix="Move to exclude"
                middle-action-prefix="Move to slick"
                right-action-prefix="Move to include"
                :show-directional-cursor="true"
                :enable-bounce="true"
                scroll-mode="parent"
                :styled-items-by-name="slickStylesByName"
                @update:states="emit('update:slickFilterStates', $event)"
              />
            </section>

            <section
              v-if="availableCardTypeLabels.length > 0"
              class="board-card-filter-section"
            >
              <TagTriStateMatrix
                :available-tag-names="availableCardTypeLabels"
                :states="cardTypeFilterStates"
                :labels="{ left: 'Exclude', middle: 'Type', right: 'Include' }"
                :ariaLabel="'Card type filter matrix'"
                semantic-scope="card"
                left-action-prefix="Move to exclude"
                middle-action-prefix="Move to card type"
                right-action-prefix="Move to include"
                :show-directional-cursor="true"
                :enable-bounce="true"
                scroll-mode="parent"
                :state-keys-by-name="cardTypeStateKeysByLabel"
                :styled-items-by-name="cardTypeStylesByLabel"
                @update:states="emit('update:cardTypeFilterStates', $event)"
              />
            </section>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Filter } from 'lucide-vue-next';
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { CardType, Slick } from '../../shared/types/boardTypes';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import type { StylePresentation } from '../../shared/utils/styleTypes';
import { useClickOutside } from '../../shared/composables/useClickOutside';
import TagTriStateMatrix from './TagTriStateMatrix.vue';

const props = defineProps<{
  availableTagNames: string[];
  availableSlicks: Slick[];
  availableCardTypes: CardType[];
  tagFilterStates: TagFilterStateMap;
  slickFilterStates: TagFilterStateMap;
  cardTypeFilterStates: TagFilterStateMap;
  hasActiveFilters: boolean;
  open: boolean;
}>();

const emit = defineEmits<{
  'update:tagFilterStates': [next: TagFilterStateMap];
  'update:slickFilterStates': [next: TagFilterStateMap];
  'update:cardTypeFilterStates': [next: TagFilterStateMap];
  'update:open': [open: boolean];
}>();

const menuId = 'board-card-filter-menu';
const dropdownRoot = ref<HTMLElement | null>(null);
const menuRef = ref<HTMLElement | null>(null);
const menuShiftPx = ref(0);

const availableSlickNames = computed(() =>
  props.availableSlicks
    .map(slick => slick.name)
    .sort((left, right) => left.localeCompare(right))
);

const slickStylesByName = computed<Record<string, StylePresentation>>(() => {
  const byName: Record<string, StylePresentation> = {};
  for (const slick of props.availableSlicks) {
    const normalisedSlickName = normaliseName(slick.name);
    if (!normalisedSlickName) {
      continue;
    }

    byName[normalisedSlickName] = {
      styleName: slick.styleName,
      stylePropertiesJson: slick.stylePropertiesJson
    };
  }

  return byName;
});

const availableCardTypeLabels = computed(() =>
  props.availableCardTypes
    .map(formatCardTypeLabel)
    .sort((left, right) => left.localeCompare(right))
);

const cardTypeStateKeysByLabel = computed<Record<string, string>>(() => {
  const byLabel: Record<string, string> = {};
  for (const cardType of props.availableCardTypes) {
    byLabel[normaliseName(formatCardTypeLabel(cardType))] = String(cardType.id);
  }

  return byLabel;
});

const cardTypeStylesByLabel = computed<Record<string, StylePresentation>>(() => {
  const byLabel: Record<string, StylePresentation> = {};
  for (const cardType of props.availableCardTypes) {
    byLabel[normaliseName(formatCardTypeLabel(cardType))] = {
      styleName: cardType.styleName,
      stylePropertiesJson: cardType.stylePropertiesJson
    };
  }

  return byLabel;
});

const menuStyle = computed(() => ({
  '--bo-card-filter-shift-x': `${menuShiftPx.value}px`
}));

function updateMenuShift() {
  const menu = menuRef.value;
  if (!menu) {
    menuShiftPx.value = 0;
    return;
  }

  const viewportPadding = 12;
  const rect = menu.getBoundingClientRect();
  let shift = 0;
  if (rect.left < viewportPadding) {
    shift = viewportPadding - rect.left;
  } else if (rect.right > window.innerWidth - viewportPadding) {
    shift = (window.innerWidth - viewportPadding) - rect.right;
  }

  menuShiftPx.value = Math.round(shift);
}

watch(() => props.open, async isOpen => {
  if (!isOpen) {
    menuShiftPx.value = 0;
    return;
  }

  await nextTick();
  updateMenuShift();
});

watch(() => props.availableTagNames.length, async () => {
  if (!props.open) {
    return;
  }

  await nextTick();
  updateMenuShift();
});

watch(() => props.availableSlicks.length, async () => {
  if (!props.open) {
    return;
  }

  await nextTick();
  updateMenuShift();
});

watch(() => props.availableCardTypes.length, async () => {
  if (!props.open) {
    return;
  }

  await nextTick();
  updateMenuShift();
});

onMounted(() => {
  window.addEventListener('resize', updateMenuShift);
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateMenuShift);
});

useClickOutside(dropdownRoot, () => {
  emit('update:open', false);
}, () => props.open);

function normaliseName(name: string) {
  return name.trim().toLocaleLowerCase();
}

function formatCardTypeLabel(cardType: CardType) {
  return cardType.emoji ? `${cardType.emoji} ${cardType.name}` : cardType.name;
}
</script>

<style scoped>
.board-card-filter-dropdown {
  position: relative;
  flex: 0 0 auto;
  width: fit-content;
  max-width: 100%;
}

.board-card-filter-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.32rem;
  min-height: var(--bo-board-filter-control-height, 2.3rem);
  padding: 0 0.65rem;
  transition: border-color 140ms ease, background-color 140ms ease, color 140ms ease;
}

.board-card-filter-toggle--active {
  --bo-btn-bg: var(--bo-colour-energy);
  --bo-btn-border: var(--bo-surface-energy);
  --bo-btn-ink: var(--bo-surface-energy);
  --bo-btn-bg-hover: var(--bo-colour-energy-strong);
  --bo-btn-border-hover: var(--bo-surface-energy);
  --bo-btn-ink-hover: var(--bo-surface-energy);
}

.board-card-filter-trigger-row {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.board-card-filter-button-wrap {
  position: relative;
}

.board-card-filter-menu {
  position: absolute;
  top: calc(100% + 0.35rem);
  left: 50%;
  transform: translateX(calc(-50% + var(--bo-card-filter-shift-x, 0px)));
  z-index: 12;
  background: var(--bo-surface-base);
  padding: 0;
  width: max-content;
  min-width: max-content;
  max-width: calc(100vw - 3.5rem);
  max-height: min(56vh, 420px);
  overflow: auto;
  box-shadow: var(--bo-shadow-pop);
}

.board-card-filter-menu-content {
  display: grid;
  gap: 0;
  align-content: start;
  padding: 0;
  width: max-content;
  min-width: max-content;
}

.board-card-filter-section {
  display: grid;
  gap: 0.3rem;
}

.board-card-filter-section :deep(.tag-tri-state) {
  border: 0;
  border-radius: 0;
}

@media (max-width: 720px) {
  .board-card-filter-dropdown {
    min-width: 0;
  }

  .board-card-filter-toggle {
    min-height: var(--bo-board-filter-control-height, 2.3rem);
    padding: 0 0.5rem;
    min-width: 2rem;
    justify-content: center;
  }

  .board-card-filter-toggle-label {
    display: none;
  }

  .board-card-filter-menu {
    left: 50%;
    right: auto;
    transform: translateX(calc(-50% + var(--bo-card-filter-shift-x, 0px)));
    width: min(21rem, calc(100vw - 1.5rem));
    max-width: calc(100vw - 1.5rem);
  }

  .board-card-filter-menu-content {
    width: 100%;
    min-width: 0;
  }
}
</style>
