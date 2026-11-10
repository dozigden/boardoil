<template>
  <FormDialog :definition="definition" @close="closeDialog" @saved="openTransferredCard">
    <template #before-fields>
      <section v-if="sourceCard" class="card-transfer-source" aria-label="Card being moved">
        <CardPreview :card="sourceCard" :column-id="sourceCard.boardColumnId" :interactive="false" />
      </section>
      <p class="card-transfer-summary">The card will receive a new number on its destination board.</p>
    </template>
  </FormDialog>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import FormDialog from '../../shared/forms/FormDialog.vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useBoardCatalogueStore } from '../../shared/stores/boardCatalogueStore';
import type { CardTransferResult } from '../../shared/types/boardTypes';
import { useCardStore } from '../stores/cardStore';
import { createCardTransferForm } from '../forms/cardTransferForm';
import CardPreview from './Card.vue';
import { getDestinationBoards } from './cardTransferSelection';

const route = useRoute();
const router = useRouter();
const api = createBoardApi();
const boardCatalogueStore = useBoardCatalogueStore();
const cardStore = useCardStore();
const { boards } = storeToRefs(boardCatalogueStore);
const sourceBoardId = parseRouteNumber(route.params.boardId);
const sourceCardId = parseRouteNumber(route.params.cardId);
const sourceCard = computed(() => cardStore.getCardById(sourceCardId));
const definition = createCardTransferForm({
  boards: () => getDestinationBoards(sourceBoardId, boards.value),
  loadBoards: () => boardCatalogueStore.loadBoards({ suppressError: true }),
  loadColumns: boardId => api.getColumns(boardId),
  transfer: (boardId, columnId, policy) => cardStore.transferCard(sourceCardId!, boardId, columnId, policy),
  isCurrent: () => sourceCardId !== null && cardStore.activeBoardId === sourceBoardId
});

async function openTransferredCard(result: CardTransferResult) {
  await router.push({ name: 'board-card', params: { boardId: result.boardId, cardId: result.card.id } });
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

</script>

<style scoped>
.card-transfer-source { margin-bottom: 0.35rem; }
.card-transfer-summary { margin: 0 0 1rem; color: var(--bo-ink-muted); }
</style>
