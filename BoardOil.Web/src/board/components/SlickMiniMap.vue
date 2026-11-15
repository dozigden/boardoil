<template>
  <section
    ref="mapElement"
    class="slick-mini-map"
    aria-label="Slick mini map"
    @keydown.esc="dismissPreview"
  >
    <div v-if="occupiedColumns.length" ref="scrollElement" class="slick-mini-map-scroll">
      <div class="slick-mini-map-columns" :style="layoutStyle">
        <div v-for="(column, index) in occupiedColumns" :key="column.id" class="slick-mini-map-column" role="group" :aria-label="column.title">
          <span
            v-if="index > 0"
            class="slick-mini-map-bridge"
            :class="slickClasses"
            :style="slickStyle"
            aria-hidden="true"
          />
          <button
            v-for="card in column.cards"
            :key="card.id"
            type="button"
            class="btn slick-mini-map-dot"
            :aria-label="`#${card.id}: ${card.title}`"
            :aria-current="card.id === currentCardId ? 'true' : undefined"
            :aria-describedby="previewCard?.id === card.id ? previewId : undefined"
            :aria-description="card.id === draftCard?.id ? 'Unsaved changes' : undefined"
            @mouseenter="hoverCard(card.id)"
            @mouseleave="clearHoveredCard"
            @focus="focusCard(card.id)"
            @blur="clearFocusedCard"
            @click="openCard(card.id)"
          >
            <span class="slick-mini-map-blot" :class="slickClasses" :style="slickStyle" aria-hidden="true" />
          </button>
        </div>
      </div>
    </div>
    <p v-else class="slick-mini-map-empty">No saved cards in this slick yet.</p>
    <div :id="previewId" ref="previewElement" popover="manual" role="tooltip" class="slick-mini-map-preview">
      <CardPreview v-if="previewCard" :card="previewCard" :column-id="previewCard.boardColumnId" :interactive="false">
        <template #footer>
          <p class="slick-mini-map-preview-column">
            {{ previewColumnTitle }}<span v-if="previewCard.id === draftCard?.id"> · Unsaved changes</span>
          </p>
        </template>
      </CardPreview>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, useId, watch } from 'vue';
import type { BoardColumn, Card, Slick } from '../../shared/types/boardTypes';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';
import { projectSlickMiniMapDraft } from '../utils/slickMiniMapDraft';
import CardPreview from './Card.vue';

const props = defineProps<{
  slick: Slick | null;
  columns: BoardColumn[];
  currentCardId: number | null;
  draftCard: Card | null;
  cohesionEnabled: boolean;
}>();
const emit = defineEmits<{ 'open-card': [cardId: number] }>();
const mapElement = ref<HTMLElement | null>(null);
const scrollElement = ref<HTMLElement | null>(null);
const previewElement = ref<HTMLElement | null>(null);
const hoveredCardId = ref<number | null>(null);
const focusedCardId = ref<number | null>(null);
const previewId = `slick-mini-map-preview-${useId()}`;
const occupiedColumns = computed(() => {
  const slickId = props.slick?.id;
  const columns = projectSlickMiniMapDraft(props.columns, props.draftCard, props.cohesionEnabled);
  return columns
    .map(column => ({
      ...column,
      cards: column.cards.filter(card => card.id === props.draftCard?.id || (slickId !== undefined && card.slickId === slickId))
    }))
    .filter(column => column.cards.length > 0);
});
const previewCard = computed(() => {
  const cardId = hoveredCardId.value ?? focusedCardId.value;
  return occupiedColumns.value.flatMap(column => column.cards).find(card => card.id === cardId) ?? null;
});
const previewColumnTitle = computed(() => props.columns.find(column => column.id === previewCard.value?.boardColumnId)?.title);
const layoutStyle = computed(() => {
  const tallestColumn = Math.max(1, ...occupiedColumns.value.map(column => column.cards.length));
  const spacing = Math.max(20, Math.min(32, 120 / tallestColumn));
  return {
    '--slick-mini-map-columns': occupiedColumns.value.length,
    '--slick-mini-map-spacing': `${spacing}px`
  };
});
const slickClasses = computed(() => getSemanticStyleClasses(props.slick, 'slick'));
const slickStyle = computed(() => getSurfaceStyle(props.slick, {
  fallbackBackground: 'var(--bo-colour-brand)',
  fallbackColor: 'var(--bo-ink-strong)',
  fallbackBorderColor: 'var(--bo-border-soft)'
}));

function clearHoveredCard() {
  hoveredCardId.value = null;
}

function clearFocusedCard() {
  focusedCardId.value = null;
}

function hoverCard(cardId: number) {
  clearFocusedCard();
  hoveredCardId.value = cardId;
}

function focusCard(cardId: number) {
  clearFocusedCard();
  if (document.activeElement?.matches(':focus-visible')) {
    clearHoveredCard();
    focusedCardId.value = cardId;
  }
}

function hidePreview() {
  clearHoveredCard();
  clearFocusedCard();
}

function dismissPreview(event: KeyboardEvent) {
  if (!previewCard.value) { return; }
  event.preventDefault();
  event.stopPropagation();
  hidePreview();
}

function openCard(cardId: number) {
  hidePreview();
  emit('open-card', cardId);
}

async function positionPreview() {
  await nextTick();
  const preview = previewElement.value;
  const map = mapElement.value;
  if (!preview || !map) { return; }
  if (!previewCard.value) {
    preview.hidePopover();
    return;
  }

  preview.showPopover();
  const bounds = map.getBoundingClientRect();
  const gap = 8;
  const width = preview.offsetWidth;
  const height = preview.offsetHeight;
  let left = bounds.left - width - gap;
  let top = bounds.top;
  if (left < gap) {
    left = Math.min(bounds.left, window.innerWidth - width - gap);
    top = bounds.bottom + gap;
    if (top + height > window.innerHeight - gap) {
      top = bounds.top - height - gap;
    }
  }
  preview.style.left = `${Math.max(gap, left)}px`;
  preview.style.top = `${Math.max(gap, Math.min(top, window.innerHeight - height - gap))}px`;
}

async function revealCurrentCard() {
  await nextTick();
  const scroll = scrollElement.value;
  const node = scroll?.querySelector<HTMLElement>('[aria-current="true"]');
  if (!scroll || !node) { return; }
  const viewport = scroll.getBoundingClientRect();
  const bounds = node.getBoundingClientRect();
  const top = viewport.top + scroll.clientTop;
  const left = viewport.left + scroll.clientLeft;
  const bottom = top + scroll.clientHeight;
  const right = left + scroll.clientWidth;
  if (bounds.top < top) { scroll.scrollTop += bounds.top - top; }
  else if (bounds.bottom > bottom) { scroll.scrollTop += bounds.bottom - bottom; }
  if (bounds.left < left) { scroll.scrollLeft += bounds.left - left; }
  else if (bounds.right > right) { scroll.scrollLeft += bounds.right - right; }
}

defineExpose({ revealCurrentCard });
watch(previewCard, positionPreview, { flush: 'post' });
watch([
  () => props.currentCardId,
  () => props.draftCard?.boardColumnId,
  () => props.slick?.id
], revealCurrentCard, { flush: 'post', immediate: true });
onMounted(() => {
  window.addEventListener('resize', positionPreview);
  document.addEventListener('scroll', positionPreview, true);
});
onBeforeUnmount(() => {
  window.removeEventListener('resize', positionPreview);
  document.removeEventListener('scroll', positionPreview, true);
});
</script>

<style scoped>
.slick-mini-map {
  min-width: 0;
}

.slick-mini-map-scroll {
  box-sizing: content-box;
  max-height: 120px;
  overflow: auto;
  border: 1px solid var(--bo-border-soft);
  border-radius: 8px;
  background: var(--bo-surface-base);
  padding: 0.35rem;
}

.slick-mini-map-columns {
  position: relative;
  isolation: isolate;
  display: grid;
  grid-template-columns: repeat(var(--slick-mini-map-columns), calc(var(--slick-mini-map-spacing) * 1.75));
  width: max-content;
  margin-inline: auto;
}

.slick-mini-map-column {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 0;
}

.slick-mini-map-bridge {
  position: absolute;
  z-index: 0;
  top: 0;
  left: -50%;
  width: 100%;
  height: var(--slick-mini-map-spacing);
  mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 32' preserveAspectRatio='none'%3E%3Cpath d='M0 5C12 5 12 13 30 13H70C88 13 88 5 100 5V27C88 27 88 19 70 19H30C12 19 12 27 0 27Z'/%3E%3C/svg%3E") center / 100% 100% no-repeat;
  pointer-events: none;
}

.btn.slick-mini-map-dot {
  position: relative;
  z-index: 1;
  display: grid;
  place-items: center;
  flex: 0 0 var(--slick-mini-map-spacing);
  width: 32px;
  height: var(--slick-mini-map-spacing);
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  box-shadow: none;
}

.slick-mini-map-blot {
  position: relative;
  display: block;
  width: calc(var(--slick-mini-map-spacing) * 0.625);
  height: calc(var(--slick-mini-map-spacing) * 0.6875);
  border-radius: 43% 57% 61% 39% / 54% 43% 57% 46%;
}

.slick-mini-map-dot:nth-child(odd) .slick-mini-map-blot {
  border-radius: 57% 43% 42% 58% / 42% 59% 41% 58%;
}

.slick-mini-map-dot + .slick-mini-map-dot .slick-mini-map-blot::before {
  content: '';
  position: absolute;
  top: calc(var(--slick-mini-map-spacing) * -0.65625);
  left: 0;
  width: 100%;
  height: var(--slick-mini-map-spacing);
  mask: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 32'%3E%3Cpath d='M0 0C0 8 7 8 7 16S0 24 0 32H20C20 24 13 24 13 16S20 8 20 0Z'/%3E%3C/svg%3E") center / 100% 100% no-repeat;
  background: inherit;
  pointer-events: none;
}

.slick-mini-map-dot:hover,
.slick-mini-map-dot:focus-visible {
  z-index: 3;
  outline: none;
}

.slick-mini-map-dot[aria-current='true'] {
  z-index: 4;
}

.slick-mini-map-dot:hover::after,
.slick-mini-map-dot:focus-visible::after,
.slick-mini-map-dot[aria-current='true']::after {
  content: '';
  position: absolute;
  z-index: 1;
  top: 50%;
  left: 50%;
  box-sizing: border-box;
  width: calc(var(--slick-mini-map-spacing) * 0.9375);
  height: calc(var(--slick-mini-map-spacing) * 0.9375);
  transform: translate(-50%, -50%);
  border-radius: 50%;
  border: max(1px, calc(var(--slick-mini-map-spacing) * 0.0625)) solid var(--bo-focus-ring);
  pointer-events: none;
}

.slick-mini-map-dot[aria-current='true']::after {
  border-color: var(--bo-ink-strong);
}

.slick-mini-map-empty {
  margin: 0;
  font-size: 0.8rem;
  color: var(--bo-ink-muted);
}

.slick-mini-map-preview {
  position: fixed;
  inset: auto;
  width: min(18rem, calc(100vw - 16px));
  max-height: calc(100vh - 16px);
  overflow: auto;
  margin: 0;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--bo-ink-strong);
  border-radius: 12px;
  box-shadow: 0 5px 20px #0003;
}

.slick-mini-map-preview :deep(.card) {
  margin: 0;
}

.slick-mini-map-preview-column {
  margin: 0.4rem 0 0;
  padding-right: var(--card-checklist-counts-space);
  font-size: 0.75rem;
  opacity: 0.8;
  overflow-wrap: anywhere;
}
</style>
