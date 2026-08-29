<template>
  <FixedChromeDialog
    class="card-transfer-dialog"
    :open="true"
    title="Move card to another board"
    close-label="Cancel moving card"
    @close="closeDialog"
    @submit="transfer"
  >
    <div class="card-transfer-body">
      <section v-if="sourceCard" class="card-transfer-source" aria-label="Card being moved">
        <CardPreview
          :card="sourceCard"
          :column-id="sourceCard.boardColumnId"
          :interactive="false"
        />
      </section>

      <p class="card-transfer-summary">
        The card will receive a new number on its destination board.
      </p>

      <section class="card-transfer-field">
        <label for="card-transfer-board">Destination board</label>
        <select
          id="card-transfer-board"
          :value="destinationBoardValue"
          :disabled="isBusy"
          @change="onDestinationBoardChange"
        >
          <option value="">{{ boardPlaceholder }}</option>
          <option v-for="boardOption in destinationBoards" :key="boardOption.id" :value="String(boardOption.id)">
            {{ boardOption.name }}
          </option>
        </select>
      </section>

      <section class="card-transfer-field">
        <label for="card-transfer-column">Destination column</label>
        <select
          id="card-transfer-column"
          :value="destinationColumnValue"
          :disabled="destinationBoardId === null || columnsBusy || isBusy"
          @change="onDestinationColumnChange"
        >
          <option v-if="destinationColumns.length === 0" value="">{{ columnPlaceholder }}</option>
          <option v-for="column in destinationColumns" :key="column.id" :value="String(column.id)">
            {{ column.title }}
          </option>
        </select>
      </section>

      <fieldset class="card-transfer-policies" :disabled="isBusy">
        <legend>Board-specific content</legend>
        <label class="card-transfer-policy">
          <input v-model="transferPolicy" type="radio" value="destinationDefaults">
          <span>
            <strong>Use destination defaults</strong>
            <small>Use the default card type and remove tags and slick.</small>
          </span>
        </label>
        <label class="card-transfer-policy">
          <input v-model="transferPolicy" type="radio" value="keepMatching">
          <span>
            <strong>Keep matching only</strong>
            <small>Use the destination card type, tags, and slick where they match, otherwise they're cleared.</small>
          </span>
        </label>
        <label class="card-transfer-policy" :class="{ 'card-transfer-policy--disabled': !canCopyMissing }">
          <input v-model="transferPolicy" type="radio" value="copyMissing" :disabled="!canCopyMissing || isBusy">
          <span>
            <strong>Copy missing</strong>
            <small>Any card type, tags, or slick that are missing will be created on the destination board.</small>
          </span>
        </label>
      </fieldset>

      <p v-if="errorMessage" class="card-transfer-error" role="alert">{{ errorMessage }}</p>
    </div>

    <template #actions>
      <section class="fixed-chrome-dialog-actions">
        <div class="fixed-chrome-dialog-actions-left">
          <button type="button" class="btn btn--secondary" :disabled="isBusy" @click="closeDialog">
            Cancel
          </button>
        </div>
        <button type="submit" class="btn" :disabled="!canSubmit">
          {{ isBusy ? 'Moving...' : 'Move card' }}
        </button>
      </section>
    </template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import type { CardTransferPolicy, Column } from '../../shared/types/boardTypes';
import { useCardStore } from '../stores/cardStore';
import CardPreview from './Card.vue';
import {
  canCopyMissingDefinitions,
  defaultCardTransferPolicy,
  getDestinationBoards,
  resolvePolicyForDestination
} from './cardTransferSelection';

const route = useRoute();
const router = useRouter();
const api = createBoardApi();
const boardCatalogueStore = useBoardCatalogueStore();
const cardStore = useCardStore();
const { boards, busy: boardsBusy } = storeToRefs(boardCatalogueStore);
const { busy: cardBusy } = storeToRefs(cardStore);

const sourceBoardId = parseRouteNumber(route.params.boardId);
const sourceCardId = parseRouteNumber(route.params.cardId);
const destinationBoardId = ref<number | null>(null);
const destinationColumnId = ref<number | null>(null);
const destinationColumns = ref<Column[]>([]);
const columnsBusy = ref(false);
const errorMessage = ref('');
const transferPolicy = ref<CardTransferPolicy>(defaultCardTransferPolicy);
let columnLoadVersion = 0;

const sourceCard = computed(() => cardStore.getCardById(sourceCardId));
const destinationBoards = computed(() => getDestinationBoards(sourceBoardId, boards.value));
const selectedDestinationBoard = computed(() => {
  return destinationBoards.value.find(board => board.id === destinationBoardId.value) ?? null;
});
const canCopyMissing = computed(() => canCopyMissingDefinitions(selectedDestinationBoard.value));
const destinationBoardValue = computed(() => destinationBoardId.value === null ? '' : String(destinationBoardId.value));
const destinationColumnValue = computed(() => destinationColumnId.value === null ? '' : String(destinationColumnId.value));
const isBusy = computed(() => boardsBusy.value || columnsBusy.value || cardBusy.value);
const boardPlaceholder = computed(() => {
  if (boardsBusy.value) {
    return 'Loading boards...';
  }

  if (destinationBoards.value.length === 0) {
    return 'No destination boards available';
  }

  return 'Select board';
});
const columnPlaceholder = computed(() => {
  if (destinationBoardId.value === null) {
    return 'Select a board first';
  }

  if (columnsBusy.value) {
    return 'Loading columns...';
  }

  if (destinationColumns.value.length === 0) {
    return 'No columns available';
  }

  return '';
});
const canSubmit = computed(() => {
  if (isBusy.value || sourceBoardId === null || sourceCardId === null) {
    return false;
  }

  if (destinationBoardId.value === null || destinationColumnId.value === null) {
    return false;
  }

  return transferPolicy.value !== 'copyMissing' || canCopyMissing.value;
});

onMounted(async () => {
  const loaded = await boardCatalogueStore.loadBoards();
  if (!loaded) {
    errorMessage.value = 'Could not load destination boards.';
  }
});

async function onDestinationBoardChange(event: Event) {
  const loadVersion = ++columnLoadVersion;
  destinationBoardId.value = parseSelectNumber(event);
  destinationColumnId.value = null;
  destinationColumns.value = [];
  columnsBusy.value = false;
  errorMessage.value = '';
  transferPolicy.value = resolvePolicyForDestination(transferPolicy.value, selectedDestinationBoard.value);

  const boardId = destinationBoardId.value;
  if (boardId === null) {
    return;
  }

  columnsBusy.value = true;
  try {
    const result = await api.getColumns(boardId);
    if (!result.ok) {
      if (loadVersion === columnLoadVersion && destinationBoardId.value === boardId) {
        errorMessage.value = result.error.message;
      }
      return;
    }

    if (loadVersion === columnLoadVersion && destinationBoardId.value === boardId) {
      destinationColumns.value = result.data;
      destinationColumnId.value = result.data[0]?.id ?? null;
    }
  } finally {
    if (loadVersion === columnLoadVersion) {
      columnsBusy.value = false;
    }
  }
}

function onDestinationColumnChange(event: Event) {
  destinationColumnId.value = parseSelectNumber(event);
  errorMessage.value = '';
}

async function transfer() {
  if (!canSubmit.value || sourceCardId === null || destinationBoardId.value === null || destinationColumnId.value === null) {
    return;
  }

  errorMessage.value = '';
  const result = await cardStore.transferCard(
    sourceCardId,
    destinationBoardId.value,
    destinationColumnId.value,
    transferPolicy.value
  );
  if (!result.ok) {
    errorMessage.value = result.error.message;
    return;
  }

  await router.push({
    name: 'board-card',
    params: { boardId: result.data.boardId, cardId: result.data.card.id }
  });
}

async function closeDialog() {
  if (sourceBoardId === null) {
    await router.push({ name: 'boards' });
    return;
  }

  if (sourceCardId !== null) {
    await router.push({
      name: 'board-card',
      params: { boardId: sourceBoardId, cardId: sourceCardId }
    });
    return;
  }

  await router.push({ name: 'board', params: { boardId: sourceBoardId } });
}

function parseRouteNumber(value: unknown) {
  const rawValue = Array.isArray(value) ? value[0] : value;
  const parsed = typeof rawValue === 'string' ? Number.parseInt(rawValue, 10) : Number.NaN;
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

function parseSelectNumber(event: Event) {
  const value = (event.target as HTMLSelectElement).value;
  if (value.length === 0) {
    return null;
  }

  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}
</script>

<style scoped>
.card-transfer-body {
  min-width: 0;
}

.card-transfer-source {
  margin-bottom: 0.35rem;
}

.card-transfer-summary {
  margin: 0 0 1rem;
  color: var(--bo-ink-muted);
}

.card-transfer-field {
  display: grid;
  gap: 0.3rem;
  margin-bottom: 0.85rem;
}

.card-transfer-field label,
.card-transfer-policies legend {
  color: var(--bo-ink-muted);
  font-size: 0.85rem;
}

.card-transfer-field select {
  min-height: 2.1rem;
}

.card-transfer-policies {
  display: grid;
  gap: 0.5rem;
  margin: 0.25rem 0 0.85rem;
  padding: 0;
  border: 0;
}

.card-transfer-policy {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 0.55rem;
  align-items: start;
  padding: 0.65rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 8px;
  cursor: pointer;
}

.card-transfer-policy input {
  margin-top: 0.2rem;
}

.card-transfer-policy span {
  display: grid;
  gap: 0.15rem;
}

.card-transfer-policy small {
  color: var(--bo-ink-muted);
}

.card-transfer-policy--disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

.card-transfer-error {
  margin: 0 0 0.75rem;
  color: var(--bo-colour-danger-ink);
}
</style>
