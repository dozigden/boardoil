<template>
  <svg width="0" height="0" class="goo-filter-defs" aria-hidden="true" focusable="false">
    <defs>
      <filter id="goo">
        <feGaussianBlur in="SourceGraphic" :stdDeviation="gooBlurStdDeviation" result="blur" />
        <feColorMatrix
          in="blur"
          type="matrix"
          :values="gooColorMatrixValues"
          result="goo"
        />
      </filter>
    </defs>
  </svg>

  <div class="board-viewport">
    <section class="board" :class="{ 'board--liquid': experimentalFeaturesEnabled && experimentalGooMode === 'liquid' }" :ref="setBoardRef">
      <GooLayer
        v-if="!experimentalFeaturesEnabled && gooRendererMode === 'full'"
        :goo-renderer-mode="gooRendererMode"
        :groups="gooGroups"
        :blob-border-radius-px="gooBlobBorderRadiusPx"
      />
      <GooLayer
        v-if="gooRendererMode === 'full' && selectionGooGroup"
        :goo-renderer-mode="gooRendererMode"
        :groups="[selectionGooGroup]"
        :blob-border-radius-px="selectionGooBlobBorderRadiusPx"
        layer-class="goo-layer--selection"
        blob-class="goo-blob--selection"
      />
      <div v-for="column in renderedColumns" :key="column.id" class="column-stack">
        <article
          class="column"
          :aria-label="`${column.title} column`"
          @dragover.prevent="event => handleColumnDragOver(column.id, event)"
          @drop.prevent="handleColumnDrop(column.id)"
        >
          <BoardColumnHeader
            :column-id="column.id"
            :title="column.title"
            :count-label="formatColumnCardCount(getVisibleCardIds(column.id).length)"
            :card-types="cardTypes"
            :selection-mode="isCardSelectionMode"
            :disable-select-all="!canSelectAllVisibleInColumn(column.id)"
            :disable-clear-visible="!canClearVisibleInColumn(column.id)"
            @open-default-card-draft="openDefaultCardDraft"
            @open-card-draft-for-type="openNewCardDraft"
            @open-card-details="openCardDetails"
            @select-all-visible="selectAllVisibleInColumn"
            @clear-visible="clearVisibleInColumn"
          />

          <div
            class="column-content"
            :data-column-id="column.id"
            :class="{
              'column-content--drop-tail': isDropPoint(column.id, null),
              'column-content--drop-head': isDropAtColumnStart(column.id)
            }"
          >
            <LiteGooLayer
              v-if="!experimentalFeaturesEnabled && gooRendererMode === 'lite'"
              :groups="gooGroupsByColumnId.get(column.id) ?? []"
              :blob-border-radius-px="gooBlobBorderRadiusPx"
              layer-class="goo-layer--column"
            />
            <LiteGooLayer
              v-if="gooRendererMode === 'lite'"
              :groups="selectionGooGroupsByColumnId.get(column.id) ?? []"
              :blob-border-radius-px="selectionGooBlobBorderRadiusPx"
              layer-class="goo-layer--column goo-layer--selection"
              blob-class="goo-blob--selection"
            />
            <Transition
              :css="false"
              @enter="motion.creationTransition.enter"
              @leave="motion.creationTransition.leave"
              @enter-cancelled="motion.creationTransition.cancel"
              @leave-cancelled="motion.creationTransition.cancel"
            >
              <div
                v-if="newCardDrafts[column.id] !== undefined"
                class="inline-card-slot"
                :data-inline-column-id="column.id"
              >
                <CreateCardInline
                  :title="newCardDrafts[column.id]?.title ?? ''"
                  :card-type-id="newCardDrafts[column.id]?.cardTypeId ?? defaultCreateCardTypeId"
                  :error-message="newCardDraftErrors[column.id] ?? ''"
                  :input-ref="element => setNewCardDraftInput(column.id, element)"
                  @update:title="title => updateNewCardDraftTitle(column.id, title)"
                  @layout-change="motion.draftLayoutChanged"
                  @save="saveNewCardDraft(column.id)"
                  @cancel="closeNewCardDraft(column.id)"
                />
              </div>
            </Transition>

            <Card
              v-for="(card, cardIndex) in column.cards"
              :key="card.id"
              :class="motion.resolveCardBoundaryClass(column.cards, cardIndex)"
              :removing="motion.isRemovingCard(card.id)"
              :board-id="boardId"
              :card="card"
              :thumbnail-attachment="cardAttachmentThumbnailStore.getForCard(card.id)"
              :column-id="column.id"
              :data-card-id="card.id"
              :data-card-sort-key="card.sortKey"
              :drop-indicator="resolveCardDropIndicator(column.id, card.id)"
              :selection-mode="isCardSelectionMode"
              :selected="isCardSelected(card.id)"
              :selected-count="selectedCardCount"
              @start-drag="onCardDragStart"
              @end-drag="onCardDragEnd"
              @dragover.prevent.stop="(event: DragEvent) => onCardDragOver(column.id, card.id, event)"
              @drop.prevent.stop="(event: DragEvent) => onCardDrop(column.id, card.id, event)"
              @edit-card="editCard"
              @toggle-select="toggleCardSelection"
            />

            <p v-if="hasActiveCardFilters && getVisibleCardIds(column.id).length === 0 && newCardDrafts[column.id] === undefined" class="column-filter-empty">
              No matching cards.
            </p>
          </div>
        </article>

        <div
          class="column-tail-drop-zone"
          :class="{ 'column-tail-drop-zone--active': isDropPoint(column.id, null) }"
          aria-hidden="true"
          @dragover.prevent.stop="onColumnTailDragOver(column.id)"
          @drop.prevent.stop="onColumnTailDrop(column.id)"
        />
      </div>
      <div :ref="motion.setMovementLayerRef" class="card-motion-layer" aria-hidden="true" inert />
    </section>
    <ExperimentalGooLayer
      v-if="experimentalFeaturesEnabled && boardElement"
      :board="boardElement"
      :mode="experimentalGooMode"
      :columns="renderedColumns"
      :slicks="slicks"
      :floating-cards="motion.floatingCards"
      :layout-version="motion.gooLayoutVersion.value"
    />
  </div>
  <Teleport v-if="experimentalFeaturesEnabled" defer :to="`#${boardLayoutRegistry.headerContentTargetId}`">
    <div class="experimental-goo-picker">
      <ExperimentalFeatureIcon :size="20" />
      <select id="experimental-goo-mode" v-model="experimentalGooMode" aria-label="Goo renderer">
        <option value="bridges">Bridges</option>
        <option value="liquid">Liquid</option>
      </select>
    </div>
  </Teleport>
  <CardEditorDialog
    v-if="detailedCardDraft"
    :create-draft="detailedCardDraft"
    @close="closeCardDetails"
    @created="cardDetailsCreated"
  />
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, shallowRef, watch } from 'vue';
import BoardColumnHeader from './BoardColumnHeader.vue';
import Card from './Card.vue';
import CreateCardInline from './CreateCardInline.vue';
import CardEditorDialog from './CardEditorDialog.vue';
import GooLayer from './GooLayer.vue';
import ExperimentalGooLayer from './ExperimentalGooLayer.vue';
import ExperimentalFeatureIcon from '../../shared/components/ExperimentalFeatureIcon.vue';
import { useBoardLayoutRegistry } from '../../site/layouts/boardLayoutRegistry';
import { useAuthStore } from '../../shared/stores/authStore';
import type { ExperimentalGooMode } from '../goo/types';
import LiteGooLayer from './LiteGooLayer.vue';
import { useBoardCardDragDrop } from '../composables/useBoardCardDragDrop';
import { useBoardInlineCreation } from '../composables/useBoardInlineCreation';
import { useBoardColumnClipping } from '../composables/useBoardColumnClipping';
import { useBoardMotion } from '../composables/useBoardMotion';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useCardAttachmentThumbnailStore } from '../stores/cardAttachmentThumbnailStore';
import { useSlickStore } from '../stores/slickStore';
import type { BoardColumn, CardEditModel } from '../../shared/types/boardTypes';
import type { CardSearchAndTagFilter } from '../utils/cardFilters';
import { formatColumnCardCount } from '../utils/columnCardCount';
import { resolveGooRendererMode } from '../utils/gooRendererMode';

const props = defineProps<{
  boardId: number;
  columns: BoardColumn[];
  cardFilters: CardSearchAndTagFilter;
  hasActiveCardFilters: boolean;
  selectionMode: boolean;
  selectedCardIds: number[];
  slickCohesionModeEnabled: boolean;
  dropSelectedCards: (columnId: number, targetCardId: number | null) => Promise<boolean>;
}>();
const emit = defineEmits<{
  'edit-card': [cardId: number];
  'toggle-select': [cardId: number];
  'select-all-visible': [columnId: number];
  'clear-visible': [columnId: number];
}>();

const boardLayoutRegistry = useBoardLayoutRegistry();
const { experimentalFeaturesEnabled } = storeToRefs(useAuthStore());
const experimentalGooMode = ref<ExperimentalGooMode>('bridges');
watch(experimentalFeaturesEnabled, () => { experimentalGooMode.value = 'bridges'; });
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const cardAttachmentThumbnailStore = useCardAttachmentThumbnailStore();
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { slicks } = storeToRefs(useSlickStore());
const filteredColumns = computed(() => props.columns);
const cardIdsByColumnId = computed(() => cardStore.cardIdsByColumnId);
const isCardSelectionMode = computed(() => props.selectionMode);
const selectedCardIds = computed(() => props.selectedCardIds);
const selectedCardIdSet = computed(() => new Set(selectedCardIds.value));
const selectedCardCount = computed(() => selectedCardIds.value.length);
const defaultCreateCardTypeId = computed(() => systemCardType.value?.id ?? cardTypes.value[0]?.id ?? null);
const gooRendererMode = resolveGooRendererMode();
const boardElement = shallowRef<HTMLElement | null>(null);
const motion = useBoardMotion(filteredColumns, cardIdsByColumnId, slicks, selectedCardIds, isCardSelectionMode, gooRendererMode,
  computed(() => props.cardFilters), experimentalFeaturesEnabled);
const {
  renderedColumns,
  gooGroups,
  gooGroupsByColumnId,
  gooBlurStdDeviation,
  gooBlobBorderRadiusPx,
  gooColorMatrixValues,
  selectionGooGroup,
  selectionGooGroupsByColumnId,
  selectionGooBlobBorderRadiusPx
} = motion;
const clipping = useBoardColumnClipping(renderedColumns, motion.gooLayoutVersion, motion.refreshGooLayout);
watch(experimentalGooMode, motion.refreshGooLayout, { flush: 'post' });
const {
  newCardDrafts,
  newCardDraftErrors,
  openNewCardDraft,
  openDefaultCardDraft,
  updateNewCardDraftTitle,
  closeNewCardDraft,
  setNewCardDraftInput,
  saveNewCardDraft
} = useBoardInlineCreation(defaultCreateCardTypeId, motion, focusCreatedCard);
const detailedCardDraft = ref<CardEditModel | null>(null);

function openCardDetails(columnId: number) {
  // A pending quick-add must finish before its draft can become a second create request.
  if (cardStore.busy) return;
  const inlineDraft = newCardDrafts.value[columnId];
  detailedCardDraft.value = {
    boardColumnId: columnId,
    title: inlineDraft?.title ?? '',
    cardTypeId: inlineDraft?.cardTypeId ?? defaultCreateCardTypeId.value,
    description: '',
    externalUrl: null,
    tagNames: [],
    assignedUserId: null,
    slickName: null
  };
  if (inlineDraft) closeNewCardDraft(columnId);
}

function closeCardDetails() {
  detailedCardDraft.value = null;
}

async function cardDetailsCreated(cardId: number) {
  closeCardDetails();
  await nextTick();
  await motion.revealCard(cardId);
  focusCreatedCard(cardId);
}

watch(() => props.boardId, closeCardDetails);
const interactiveColumns = computed(() => renderedColumns.value.map(column => ({
  ...column, cards: column.cards.filter(card => !motion.isRemovingCard(card.id))
})));
const {
  onCardDragStart,
  onCardDragEnd,
  onCardDragOver,
  onCardDrop,
  onColumnTailDragOver,
  onColumnTailDrop,
  handleColumnDragOver,
  handleColumnDrop,
  isDropPoint,
  isDropAtColumnStart,
  resolveCardDropIndicator,
  clearDragInteraction
} = useBoardCardDragDrop(
  interactiveColumns,
  isCardSelectionMode,
  selectedCardIds,
  cardStore.moveCard,
  props.dropSelectedCards,
  handleCrossColumnDrop
);

function setBoardRef(element: unknown) {
  boardElement.value = element instanceof HTMLElement ? element : null;
  motion.setBoardRef(element);
  clipping.setBoardRef(element);
}

function focusCreatedCard(cardId: number) {
  boardElement.value?.querySelector<HTMLElement>(`[data-card-id="${cardId}"]`)?.focus({ preventScroll: true });
}

function handleCrossColumnDrop(cardId: number) {
  if (props.slickCohesionModeEnabled) {
    void motion.revealCard(cardId);
  }
}

function isCardSelected(cardId: number) {
  return selectedCardIdSet.value.has(cardId);
}

function canSelectAllVisibleInColumn(columnId: number) {
  const column = interactiveColumns.value.find(column => column.id === columnId);
  return column?.cards.some(card => !isCardSelected(card.id)) ?? false;
}

function canClearVisibleInColumn(columnId: number) {
  const column = interactiveColumns.value.find(column => column.id === columnId);
  return column?.cards.some(card => isCardSelected(card.id)) ?? false;
}

function selectAllVisibleInColumn(columnId: number) {
  emit('select-all-visible', columnId);
}

function clearVisibleInColumn(columnId: number) {
  emit('clear-visible', columnId);
}

function editCard(cardId: number) {
  emit('edit-card', cardId);
}

function toggleCardSelection(cardId: number) {
  emit('toggle-select', cardId);
}

function getVisibleCardIds(columnId?: number) {
  return interactiveColumns.value
    .filter(column => columnId === undefined || column.id === columnId)
    .flatMap(column => column.cards.map(card => card.id));
}

defineExpose({ resetInteraction: clearDragInteraction, getVisibleCardIds });
</script>

<style scoped>
.experimental-goo-picker {
  display: flex; align-items: center; gap: .35rem; flex: 0 0 auto;
  height: 2rem; box-sizing: border-box; padding-left: .4rem;
  color: var(--bo-link); font-size: .85rem; font-weight: 650;
  background: var(--bo-surface-brand);
  border: 1px solid color-mix(in srgb, var(--bo-link) 55%, var(--bo-border-brand));
  border-radius: 8px;
}
.experimental-goo-picker:focus-within {
  border-color: var(--bo-focus-ring);
  box-shadow: 0 0 0 2px var(--bo-focus-ring-shadow);
}
.experimental-goo-picker select {
  width: auto; height: 100%; margin: 0; font: inherit; color: inherit;
  background: var(--bo-surface-brand); border: 0;
  border-radius: 0 7px 7px 0; padding: .2rem .3rem;
}

.board-viewport {
  position: relative;
  display: flex;
  flex: 1;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.board {
  --column-min-width: 280px;
  --column-max-width: 360px;
  position: relative;
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(var(--column-min-width), var(--column-max-width));
  grid-template-rows: 1fr;
  gap: var(--bo-standard-gap);
  margin-top: 0;
  align-items: stretch;
  min-height: 0;
  height: auto;
  overflow-x: auto;
  overflow-y: hidden;
  overscroll-behavior-x: contain;
  box-sizing: border-box;
  padding-inline: var(--bo-board-scroll-inline-padding, 0);
  scroll-padding-inline: var(--bo-board-scroll-inline-padding, 0);
  padding-bottom: 0;
  flex: 1;
}

.column {
  background: var(--bo-surface-panel);
  border: 1px solid var(--bo-border-soft);
  border-radius: 14px;
  position: relative;
  padding: 0.75rem 0.25rem;
  display: flex;
  flex-direction: column;
  gap: 0;
  min-height: 0;
  height: auto;
  max-height: 100%;
  overflow: hidden;
}

.column--short {
  overflow: visible;
}

.column--short .column-content {
  overflow: visible;
  margin-left: var(--column-scroll-gutter-left);
  margin-right: var(--column-scroll-gutter-right);
  clip-path: inset(0 0 -100vh 0);
}

.board > :deep(.goo-layer--full) {
  clip-path: inset(var(--board-goo-clip-top, 0px) 0 var(--board-goo-clip-bottom, 0px) 0);
}

.column-stack {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  position: relative;
}

.column > * {
  position: relative;
  z-index: 3;
}

.column-content {
  --column-card-gap: 0.5rem;
  --column-scrollbar-width: 0.375rem;
  display: flex;
  flex-direction: column;
  flex: 1 1 auto;
  gap: var(--column-card-gap);
  min-height: 0;
  overflow-y: auto;
  padding: 0 0.5rem 5px;
  overscroll-behavior-y: none;
  scrollbar-width: thin;
  scrollbar-color: transparent transparent;
  scrollbar-gutter: stable both-edges;
  position: relative;
}

.column-content > :not(.goo-layer) {
  position: relative;
  z-index: 1;
}

.column-content--drop-tail {
  box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--bo-focus-ring) 45%, transparent);
  border-radius: 10px;
}

.column-content--drop-head::before {
  content: '';
  position: absolute;
  left: 0.25rem;
  right: 0.75rem;
  top: 0.1rem;
  height: 4px;
  border-radius: 999px;
  background: var(--bo-focus-ring);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--bo-focus-ring) 30%, transparent);
  pointer-events: none;
  z-index: 2;
}

.column-content > .card--slick-gap {
  margin-top: var(--column-card-gap);
}

.board--liquid .column-content > .card--slick-gap {
  margin-top: calc(var(--column-card-gap) * 1.5);
}

.column-content > .card--slick-gap-top {
  margin-top: var(--column-card-gap);
}

.column-content > .card--slick-gap-strong {
  margin-top: calc(var(--column-card-gap) * 2);
}

.column-content > .card {
  flex-shrink: 0;
  margin-bottom: 0;
  position: relative;
  z-index: 1;
}

.inline-card-slot {
  display: flow-root;
  flex-shrink: 0;
}

.card-motion-layer {
  position: absolute;
  inset: 0;
  pointer-events: none;
  z-index: 5;
}

.column-content > .card--slick-gap-bottom {
  margin-bottom: var(--column-card-gap);
}

.column-content:hover,
.column-content:focus-within {
  scrollbar-width: thin;
  scrollbar-color: var(--bo-border-default) transparent;
}

.column-content::-webkit-scrollbar {
  width: var(--column-scrollbar-width);
}

.column-content::-webkit-scrollbar-track {
  background: transparent;
}

.column-content::-webkit-scrollbar-thumb {
  background: transparent;
  border: 2px solid transparent;
  background-clip: content-box;
  border-radius: 999px;
}

.column-content:hover::-webkit-scrollbar-thumb,
.column-content:focus-within::-webkit-scrollbar-thumb {
  background: color-mix(in srgb, var(--bo-border-default) 78%, transparent);
}

.column-filter-empty {
  margin: 0;
  color: var(--bo-ink-subtle);
  font-size: 0.85rem;
  text-align: center;
  padding: 0.55rem;
}

.column-tail-drop-zone {
  flex: 1 1 auto;
  min-height: 0.25rem;
  border-radius: 10px;
  border: 1px dashed transparent;
  transition: background-color 120ms ease, border-color 120ms ease;
}

.column-tail-drop-zone--active {
  background: color-mix(in srgb, var(--bo-focus-ring) 14%, transparent);
  border-color: color-mix(in srgb, var(--bo-focus-ring) 60%, transparent);
}

.goo-filter-defs {
  position: absolute;
}

</style>
