<template>
  <section v-if="isOwner" class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Card Types</h2>
      <button type="button" class="btn" :disabled="busy" aria-label="Add card type" title="Add card type" @click="openCreateEditor">
        <Plus :size="16" aria-hidden="true" />
        <span>Add Card Type</span>
      </button>
    </header>

    <p v-if="cardTypes.length === 0" class="entity-rows-empty">No card types yet.</p>

    <section v-else class="entity-rows-list">
      <article v-for="cardType in cardTypes" :key="cardType.id" class="entity-row">
        <button
          type="button"
          class="entity-row-main entity-row-main-button"
          :disabled="busy"
          :aria-label="`Edit card type ${cardType.name}`"
          @click="openEditor(cardType.id)"
        >
          <span class="entity-row-title card-type-title">
            <span v-if="cardType.emoji" class="card-type-emoji" aria-hidden="true">{{ cardType.emoji }}</span>
            <span>{{ cardType.name }}</span>
          </span>
          <span class="entity-row-badges">
            <span v-if="cardType.isSystem" class="badge">System</span>
          </span>
        </button>
        <div class="entity-row-actions">
          <button
            type="button"
            class="btn btn--secondary entity-row-action-icon"
            :disabled="busy"
            aria-label="Edit card type"
            title="Edit card type"
            @click="openEditor(cardType.id)"
          >
            <Pencil :size="16" aria-hidden="true" />
          </button>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { Pencil, Plus } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';

const router = useRouter();
const route = useRoute();
const boardStore = useBoardStore();
const cardTypeStore = useCardTypeStore();
const { currentUserRole } = storeToRefs(boardStore);
const { initialize } = boardStore;
const { cardTypes, busy } = storeToRefs(cardTypeStore);
const { loadCardTypes } = cardTypeStore;

const isOwner = computed(() => currentUserRole.value === 'Owner');

const routeBoardId = computed(() => {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});

watch(
  routeBoardId,
  async nextBoardId => {
    if (nextBoardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await initialize(nextBoardId);
    if (!loaded && routeBoardId.value === nextBoardId) {
      await router.replace({ name: 'boards' });
      return;
    }

    if (!isOwner.value) {
      await router.replace({ name: 'board', params: { boardId: nextBoardId } });
      return;
    }

    await loadCardTypes(nextBoardId);
  },
  { immediate: true }
);

async function openEditor(cardTypeId: number) {
  if (routeBoardId.value === null) {
    return;
  }

  await router.push({ name: 'card-types-card-type', params: { boardId: routeBoardId.value, cardTypeId } });
}

async function openCreateEditor() {
  if (routeBoardId.value === null) {
    return;
  }

  await router.push({ name: 'card-types-new', params: { boardId: routeBoardId.value } });
}
</script>

<style scoped>
.card-type-title {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}
</style>
