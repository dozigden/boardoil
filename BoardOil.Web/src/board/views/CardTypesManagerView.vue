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
            <span v-if="cardType.emoji" class="card-type-emoji bo-emoji" aria-hidden="true">{{ cardType.emoji }}</span>
            <span>{{ cardType.name }}</span>
          </span>
          <span class="entity-row-badges">
            <span v-if="cardType.isSystem" class="badge">Default</span>
          </span>
          <span class="entity-row-card-count">{{ formatCardCount(cardTypeUsageCountById[cardType.id] ?? 0) }}</span>
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
import { MoreVertical, Plus } from '@lucide/vue';
import { storeToRefs } from 'pinia';
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import { useBoardStore } from '../stores/boardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { countCardsByCardTypeId, formatCardCount } from '../utils/cardUsageCounts';

const router = useRouter();
const boardStore = useBoardStore();
const cardTypeStore = useCardTypeStore();
const { board, currentUserRole, currentBoardId } = storeToRefs(boardStore);
const { cardTypes, busy } = storeToRefs(cardTypeStore);
const { loadCardTypes, setDefaultCardType } = cardTypeStore;

const isOwner = computed(() => currentUserRole.value === 'Owner');
const boardId = computed(() => currentBoardId.value!);
const cardTypeUsageCountById = computed<Record<number, number>>(() => countCardsByCardTypeId(board.value));

onMounted(() => {
  void initializeView();
});

async function initializeView() {
  if (!isOwner.value) {
    await router.replace({ name: 'board', params: { boardId: boardId.value } });
    return;
  }

  await loadCardTypes(boardId.value);
}

async function openEditor(cardTypeId: number) {
  await router.push({ name: 'card-types-card-type', params: { boardId: boardId.value, cardTypeId } });
}

async function openCreateEditor() {
  await router.push({ name: 'card-types-new', params: { boardId: boardId.value } });
}

async function setAsDefault(cardTypeId: number) {
  await setDefaultCardType(cardTypeId, boardId.value);
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
