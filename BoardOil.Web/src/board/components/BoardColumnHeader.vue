<template>
  <header class="column-header">
    <div class="column-heading">
      <h2 class="column-name">{{ title }}</h2>
      <span class="column-card-count">{{ countLabel }}</span>
    </div>
    <div v-if="selectionMode" class="column-selection-actions">
      <button
        type="button"
        class="btn column-selection-action"
        title="Select all visible cards in column"
        :disabled="disableSelectAll"
        @click="emit('selectAllVisible', columnId)"
      >
        All
      </button>
      <button
        type="button"
        class="btn column-selection-action"
        title="Unselect all visible cards in column"
        :disabled="disableClearVisible"
        @click="emit('clearVisible', columnId)"
      >
        None
      </button>
    </div>
    <div v-else class="btn-group">
      <button
        type="button"
        class="btn btn--secondary column-add-card column-add-card-main"
        aria-label="Add default card"
        title="Add default card"
        @click="emit('openDefaultCardDraft', columnId)"
      >
        <Plus :size="16" aria-hidden="true" />
      </button>
      <BoDropdown
        v-if="cardTypes.length > 1"
        label="Choose card type"
        :icon="ChevronDown"
        :icon-size="14"
        icon-only
        button-class="column-add-card"
        align="right"
      >
        <template #default="{ close }">
          <button
            v-for="cardType in cardTypes"
            :key="cardType.id"
            type="button"
            class="bo-dropdown-item"
            :title="`New ${cardType.name}`"
            @click="emit('openCardDraftForType', columnId, cardType.id); close()"
          >
            <span class="bo-dropdown-item-main">
              {{ cardType.emoji ? `${cardType.emoji} ${cardType.name}` : cardType.name }}
            </span>
          </button>
        </template>
      </BoDropdown>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ChevronDown, Plus } from 'lucide-vue-next';
import type { CardType } from '../../shared/types/boardTypes';
import BoDropdown from '../../shared/components/BoDropdown.vue';

defineProps<{
  columnId: number;
  title: string;
  countLabel: string;
  cardTypes: CardType[];
  selectionMode?: boolean;
  disableSelectAll?: boolean;
  disableClearVisible?: boolean;
}>();

const emit = defineEmits<{
  openDefaultCardDraft: [columnId: number];
  openCardDraftForType: [columnId: number, cardTypeId: number];
  selectAllVisible: [columnId: number];
  clearVisible: [columnId: number];
}>();
</script>

<style scoped>
.column-header {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  align-items: center;
  padding-right: 0.5rem;
}

.column-heading {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.45rem;
  min-width: 0;
}

.column-name {
  margin: 0;
  font-size: 1rem;
}

.column-card-count {
  flex: 0 0 auto;
  font-size: 0.88rem;
  font-weight: 600;
  color: var(--bo-ink-muted);
  line-height: 1.2;
}

.column-add-card {
  height: 2rem;
  min-height: 2rem;
  padding: 0;
  line-height: 1;
}

.column-add-card-main {
  min-width: 2rem;
}

.column-selection-actions {
  display: inline-flex;
  gap: 0.35rem;
}

.column-selection-action {
  min-height: 2rem;
  padding: 0 0.55rem;
  font-size: 0.84rem;
}
</style>
