<template>
  <div class="card-type-preview">
    <span class="badge">Preview</span>
    <article class="card-type-preview-card" :class="classes" :style="style" aria-label="Card type preview">
      <div class="card-header">
        <strong class="card-title">{{ title }}</strong>
        <span class="card-id">#123</span>
      </div>
    </article>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { CardTypeDraft } from '../forms/cardTypeForm';
import { buildStylePropertiesJsonFromDraft, getCardSurfaceClassList, getCardSurfaceStyle, normaliseCardTypeEmojiForRender } from '../../shared/utils/cardTypeStyles';

const props = defineProps<{ draft: CardTypeDraft; fallbackName: string }>();
const presentation = computed(() => ({ styleName: props.draft.style.styleName, stylePropertiesJson: buildStylePropertiesJsonFromDraft(props.draft.style) }));
const classes = computed(() => getCardSurfaceClassList(presentation.value));
const style = computed(() => getCardSurfaceStyle(presentation.value));
const title = computed(() => {
  const name = props.draft.name.trim() || props.fallbackName;
  const emoji = normaliseCardTypeEmojiForRender(props.draft.emoji);
  return emoji ? `${emoji} ${name}` : name;
});
</script>

<style scoped>
.card-type-preview { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
.card-type-preview-card {
  min-width: 15rem;
  border: 1px solid var(--bo-card-surface-border-color, var(--bo-border-soft));
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-card-surface-background, var(--bo-surface-base));
  color: var(--bo-card-surface-color, inherit);
}
.card-header { display: flex; align-items: flex-start; justify-content: space-between; gap: 0.5rem; }
.card-title { min-width: 0; line-height: 1.25; overflow-wrap: anywhere; }
.card-id { flex: 0 0 auto; font-weight: 600; line-height: 1.25; }
</style>
