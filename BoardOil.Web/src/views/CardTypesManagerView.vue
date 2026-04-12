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
            <span v-if="cardType.isSystem" class="badge">Default</span>
          </span>
        </button>
        <div class="entity-row-actions">
          <BoDropdown
            align="right"
            icon-only
            label="Card type actions"
            :icon="MoreVertical"
            :disabled="busy"
          >
            <template #default="{ close }">
              <button type="button" class="bo-dropdown-item" @click="openEditorFromMenu(cardType.id, close)">
                Edit
              </button>
              <button
                v-if="!cardType.isSystem"
                type="button"
                class="bo-dropdown-item"
                @click="setAsDefaultFromMenu(cardType.id, close)"
              >
                Set as default
              </button>
            </template>
          </BoDropdown>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { MoreVertical, Plus } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoDropdown from '../components/BoDropdown.vue';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';

const router = useRouter();
const route = useRoute();
const boardStore = useBoardStore();
const cardTypeStore = useCardTypeStore();
const { currentUserRole } = storeToRefs(boardStore);
const { initialize } = boardStore;
const { cardTypes, busy } = storeToRefs(cardTypeStore);
const { loadCardTypes, setDefaultCardType } = cardTypeStore;

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

async function setAsDefault(cardTypeId: number) {
  if (routeBoardId.value === null) {
    return;
  }

  await setDefaultCardType(cardTypeId, routeBoardId.value);
}

async function openEditorFromMenu(cardTypeId: number, close: () => void) {
  close();
  await openEditor(cardTypeId);
}

async function setAsDefaultFromMenu(cardTypeId: number, close: () => void) {
  close();
  await setAsDefault(cardTypeId);
}
</script>

<style scoped>
.card-type-title {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}
</style>
