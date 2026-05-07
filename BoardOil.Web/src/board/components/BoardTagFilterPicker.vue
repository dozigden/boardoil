<template>
  <div ref="dropdownRoot" class="board-tag-filter-dropdown">
    <div class="board-tag-filter-trigger-row">
      <div class="board-tag-filter-button-wrap">
        <button
          type="button"
          class="btn btn--secondary board-tag-filter-toggle"
          :class="{ 'board-tag-filter-toggle--active': hasActiveTagFilters }"
          aria-label="Tag filters"
          title="Tag filters"
          :aria-controls="menuId"
          :aria-expanded="open"
          @click="emit('update:open', !open)"
        >
          <Filter :size="14" aria-hidden="true" />
          <span class="board-tag-filter-toggle-label">Tags</span>
        </button>

        <section
          v-if="open"
          ref="menuRef"
          :id="menuId"
          class="panel panel--compact board-tag-filter-menu"
          :style="menuStyle"
          aria-label="Tag filter matrix"
        >
          <TagTriStateMatrix
            :available-tag-names="availableTagNames"
            :states="filterStates"
            :labels="{ left: 'Exclude', middle: 'Tag', right: 'Include' }"
            :ariaLabel="'Tag filter matrix'"
            left-action-prefix="Move to exclude"
            middle-action-prefix="Move to tag"
            right-action-prefix="Move to include"
            :show-directional-cursor="true"
            :enable-bounce="true"
            @update:states="emit('update:filterStates', $event)"
          />
        </section>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Filter } from 'lucide-vue-next';
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import { useClickOutside } from '../../shared/composables/useClickOutside';
import TagTriStateMatrix from './TagTriStateMatrix.vue';

const props = defineProps<{
  availableTagNames: string[];
  filterStates: TagFilterStateMap;
  hasActiveTagFilters: boolean;
  open: boolean;
}>();

const emit = defineEmits<{
  'update:filterStates': [next: TagFilterStateMap];
  'update:open': [open: boolean];
}>();

const menuId = 'board-tag-filter-menu';
const dropdownRoot = ref<HTMLElement | null>(null);
const menuRef = ref<HTMLElement | null>(null);
const menuShiftPx = ref(0);

const menuStyle = computed(() => ({
  '--bo-tag-filter-shift-x': `${menuShiftPx.value}px`
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

watch(() => props.open, async (isOpen) => {
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

onMounted(() => {
  window.addEventListener('resize', updateMenuShift);
});

onBeforeUnmount(() => {
  window.removeEventListener('resize', updateMenuShift);
});

useClickOutside(dropdownRoot, () => {
  emit('update:open', false);
}, () => props.open);
</script>

<style scoped>
.board-tag-filter-dropdown {
  position: relative;
  flex: 0 0 auto;
  width: fit-content;
  max-width: 100%;
}

.board-tag-filter-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.32rem;
  min-height: var(--bo-board-filter-control-height, 2.3rem);
  padding: 0 0.65rem;
  transition: border-color 140ms ease, background-color 140ms ease, color 140ms ease;
}

.board-tag-filter-toggle--active {
  --bo-btn-bg: var(--bo-colour-energy);
  --bo-btn-border: var(--bo-surface-energy);
  --bo-btn-ink: var(--bo-surface-energy);
  --bo-btn-bg-hover: var(--bo-colour-energy-strong);
  --bo-btn-border-hover: var(--bo-surface-energy);
  --bo-btn-ink-hover: var(--bo-surface-energy);
}

.board-tag-filter-trigger-row {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.board-tag-filter-button-wrap {
  position: relative;
}

.board-tag-filter-menu {
  position: absolute;
  top: calc(100% + 0.35rem);
  left: 50%;
  transform: translateX(calc(-50% + var(--bo-tag-filter-shift-x, 0px)));
  z-index: 12;
  background: var(--bo-surface-base);
  padding: 0;
  width: fit-content;
  max-width: calc(100vw - 3.5rem);
  max-height: min(56vh, 420px);
  overflow: auto;
  box-shadow: var(--bo-shadow-pop);
}

@media (max-width: 720px) {
  .board-tag-filter-dropdown {
    min-width: 0;
  }

  .board-tag-filter-toggle {
    min-height: var(--bo-board-filter-control-height, 2.3rem);
    padding: 0 0.5rem;
    min-width: 2rem;
    justify-content: center;
  }

  .board-tag-filter-toggle-label {
    display: none;
  }

  .board-tag-filter-menu {
    left: 50%;
    right: auto;
    transform: translateX(calc(-50% + var(--bo-tag-filter-shift-x, 0px)));
    width: min(21rem, calc(100vw - 1.5rem));
    max-width: calc(100vw - 1.5rem);
  }
}
</style>
