<template>
  <span class="card-type-badge" :class="classes" :style="style" :title="cardType.name">
    <span v-if="emoji" class="card-type-badge-emoji bo-emoji" aria-hidden="true">{{ emoji }}</span>
    <span class="card-type-badge-name">{{ cardType.name }}</span>
  </span>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { CardType } from '../../shared/types/boardTypes';
import { getCardSurfaceClassList, getCardSurfaceStyle, normaliseCardTypeEmojiForRender } from '../../shared/utils/cardTypeStyles';

const props = defineProps<{ cardType: CardType }>();
const classes = computed(() => getCardSurfaceClassList(props.cardType));
const style = computed(() => getCardSurfaceStyle(props.cardType));
const emoji = computed(() => normaliseCardTypeEmojiForRender(props.cardType.emoji));
</script>

<style scoped>
.card-type-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
  max-width: 100%;
  padding: 0.2rem 0.5rem;
  border: 1px solid var(--bo-card-surface-border-color, var(--bo-border-soft));
  border-radius: 6px;
  background: var(--bo-card-surface-background, var(--bo-surface-base));
  color: var(--bo-card-surface-color, var(--bo-ink-strong));
}

.card-type-badge-emoji {
  flex: 0 0 auto;
}

.card-type-badge-name {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
