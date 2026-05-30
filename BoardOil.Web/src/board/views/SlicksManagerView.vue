<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Slicks</h2>
      <button type="button" class="btn" :disabled="busy" aria-label="Add slick" title="Add slick" @click="openCreateEditor">
        <Plus :size="16" aria-hidden="true" />
        <span>Add Slick</span>
      </button>
    </header>

    <p v-if="slicks.length === 0" class="entity-rows-empty">No slicks yet. Add one to get started.</p>

    <section v-else class="entity-rows-list">
      <article v-for="slick in slicks" :key="slick.id" class="entity-row">
        <button
          type="button"
          class="entity-row-main entity-row-main-button"
          :disabled="busy"
          :aria-label="`Edit slick ${slick.name}`"
          @click="openEditor(slick.id)"
        >
          <span class="entity-row-title">{{ slick.name }}</span>
          <span class="entity-row-badges slicks-row-preview">
            <span class="slicks-row-swatch" :class="getSlickStyleClasses(slick)" :style="getSlickStyle(slick)">
              <span class="slicks-row-swatch-label">{{ slick.name }}</span>
            </span>
          </span>
        </button>
        <div class="entity-row-actions">
          <button
            type="button"
            class="btn btn--secondary entity-row-action-icon"
            :disabled="busy"
            aria-label="Edit slick"
            title="Edit slick"
            @click="openEditor(slick.id)"
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
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';
import { useSlickStore } from '../stores/slickStore';
import type { Slick } from '../../shared/types/boardTypes';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';

const router = useRouter();
const boardStore = useBoardStore();
const slickStore = useSlickStore();
const { currentBoardId } = storeToRefs(boardStore);
const { slicks, busy } = storeToRefs(slickStore);
const { loadSlicks } = slickStore;

const boardId = computed(() => currentBoardId.value!);

onMounted(() => {
  void initializeView();
});

function getSlickStyle(slick: Slick) {
  return getSurfaceStyle(slick, {
    fallbackBackground: 'var(--panel)',
    fallbackColor: 'var(--text)',
    fallbackBorderColor: 'var(--line)'
  });
}

function getSlickStyleClasses(slick: Slick) {
  return getSemanticStyleClasses(slick, 'slick');
}

async function openEditor(slickId: number) {
  await router.push({ name: 'slicks-slick', params: { boardId: boardId.value, slickId } });
}

async function openCreateEditor() {
  await router.push({ name: 'slicks-new', params: { boardId: boardId.value } });
}

async function initializeView() {
  await loadSlicks(boardId.value);
}
</script>

<style scoped>
.slicks-row-preview {
  min-width: 0;
}

.slicks-row-swatch {
  display: inline-flex;
  align-items: center;
  width: min(20rem, 100%);
  min-width: 7rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 6px;
  padding: 0.2rem 0.5rem;
}

.slicks-row-swatch-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
