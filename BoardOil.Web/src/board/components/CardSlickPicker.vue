<template>
  <div class="card-editor-select-field card-editor-slick-picker">
    <span class="card-editor-field-label">Slick</span>
    <BoDropdown
      align="left"
      label="Select slick"
      :teleport="false"
      :text="selectedSlickLabel"
    >
      <template #trigger>
        <span
          v-if="selectedSlick"
          class="card-editor-slick-swatch card-editor-slick-swatch--trigger"
          :class="getSlickStyleClasses(selectedSlick)"
          :style="getSlickStyle(selectedSlick)"
        >
          <span class="card-editor-slick-swatch-label">{{ selectedSlick.name }}</span>
        </span>
        <span v-else-if="hasSelectedSlickName">{{ selectedSlickLabel }}</span>
      </template>
      <template #default="{ close }">
        <div class="card-editor-slick-search">
          <input
            :value="slickSearchEntry"
            type="text"
            maxlength="40"
            placeholder="Search slicks"
            aria-label="Search slicks"
            @input="setSlickSearchEntry(($event.target as HTMLInputElement).value)"
            @keydown.enter.prevent="applySlickSearchSelection(close)"
          />
        </div>
        <button
          type="button"
          class="bo-dropdown-item"
          @click="setDraftSlickName(null, close)"
        >
          <span class="bo-dropdown-item-main">No slick</span>
          <span v-if="!hasSelectedSlickName" class="badge bo-dropdown-item-meta">Selected</span>
        </button>
        <button
          v-for="slick in filteredSlicks"
          :key="slick.id"
          type="button"
          class="bo-dropdown-item"
          @click="setDraftSlickName(slick.name, close)"
        >
          <span class="bo-dropdown-item-main card-editor-slick-option">
            <span class="card-editor-slick-swatch" :class="getSlickStyleClasses(slick)" :style="getSlickStyle(slick)">
              <span class="card-editor-slick-swatch-label">{{ slick.name }}</span>
            </span>
          </span>
          <span v-if="selectedSlick?.id === slick.id" class="badge bo-dropdown-item-meta">Selected</span>
        </button>
        <button
          v-if="canCreateSlickFromEntry"
          type="button"
          class="bo-dropdown-item card-editor-slick-create"
          @click="setDraftSlickName(trimmedSlickSearchEntry, close)"
        >
          <span class="bo-dropdown-item-main">Create and use "{{ trimmedSlickSearchEntry }}"</span>
        </button>
        <p v-if="filteredSlicks.length === 0 && !canCreateSlickFromEntry" class="card-editor-slick-empty">
          No matching slicks.
        </p>
      </template>
    </BoDropdown>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import type { Slick } from '../../shared/types/boardTypes';
import { getSemanticStyleClasses, getSurfaceStyle } from '../../shared/utils/styleRenderer';

const props = defineProps<{
  slicks: Slick[];
}>();

const slickNameModel = defineModel<string | null>('slickName', { required: true });
const slickSearchEntry = ref('');

const selectedSlick = computed<Slick | null>(() => {
  if (!hasSelectedSlickName.value) {
    return null;
  }

  const targetNormalisedName = normaliseSlickNameKey(slickNameModel.value!);
  return props.slicks.find(x => normaliseSlickNameKey(x.name) === targetNormalisedName) ?? null;
});
const hasSelectedSlickName = computed(() => normaliseSlickNameForSave(slickNameModel.value) !== null);
const selectedSlickLabel = computed(() => {
  if (!hasSelectedSlickName.value) {
    return 'No slick';
  }

  if (selectedSlick.value) {
    return selectedSlick.value.name;
  }

  return slickNameModel.value!;
});
const trimmedSlickSearchEntry = computed(() => slickSearchEntry.value.trim());
const filteredSlicks = computed(() => {
  const query = trimmedSlickSearchEntry.value;
  if (!query) {
    return props.slicks;
  }

  const normalisedQuery = normaliseSlickNameKey(query);
  return props.slicks.filter(slick => normaliseSlickNameKey(slick.name).includes(normalisedQuery));
});
const canCreateSlickFromEntry = computed(() => {
  const query = trimmedSlickSearchEntry.value;
  if (!query) {
    return false;
  }

  const normalisedQuery = normaliseSlickNameKey(query);
  const existing = props.slicks.some(slick => normaliseSlickNameKey(slick.name) === normalisedQuery);
  return !existing;
});

function setSlickSearchEntry(value: string) {
  slickSearchEntry.value = value.slice(0, 40);
}

function setDraftSlickName(slickName: string | null, close?: () => void) {
  slickNameModel.value = normaliseSlickNameForSave(slickName);
  slickSearchEntry.value = '';
  close?.();
}

function applySlickSearchSelection(close?: () => void) {
  const query = trimmedSlickSearchEntry.value;
  if (!query) {
    return;
  }

  const normalisedQuery = normaliseSlickNameKey(query);
  const exactMatch = props.slicks.find(slick => normaliseSlickNameKey(slick.name) === normalisedQuery);
  if (exactMatch) {
    setDraftSlickName(exactMatch.name, close);
    return;
  }

  const firstMatch = filteredSlicks.value[0];
  if (firstMatch) {
    setDraftSlickName(firstMatch.name, close);
    return;
  }

  if (canCreateSlickFromEntry.value) {
    setDraftSlickName(query, close);
  }
}

function normaliseSlickNameForSave(slickName: string | null) {
  if (slickName === null) {
    return null;
  }

  const canonicalName = slickName.trim();
  if (canonicalName.length === 0) {
    return null;
  }

  return canonicalName;
}

function normaliseSlickNameKey(slickName: string) {
  return slickName.trim().toUpperCase();
}

function getSlickStyle(slick: Slick) {
  return getSurfaceStyle(slick, {
    fallbackBackground: 'var(--bo-surface-base)',
    fallbackColor: 'var(--bo-ink-strong)',
    fallbackBorderColor: 'var(--bo-border-soft)'
  });
}

function getSlickStyleClasses(slick: Slick) {
  return getSemanticStyleClasses(slick, 'slick');
}
</script>

<style scoped>
.card-editor-select-field {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.card-editor-slick-picker :deep(.bo-dropdown) {
  width: 100%;
}

.card-editor-slick-picker :deep(.bo-dropdown-trigger) {
  width: 100%;
  justify-content: space-between;
}

.card-editor-slick-picker :deep(.bo-dropdown-panel) {
  width: auto;
  min-width: 11rem;
}

.card-editor-slick-option {
  display: flex;
  min-width: 0;
  flex: 1;
}

.card-editor-slick-search {
  padding: 0.15rem 0.15rem 0.35rem;
}

.card-editor-slick-search input {
  width: 100%;
  min-width: 0;
}

.card-editor-slick-swatch {
  display: inline-flex;
  align-items: center;
  width: 100%;
  min-width: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 6px;
  padding: 0.2rem 0.5rem;
  max-width: 100%;
}

.card-editor-slick-swatch-label {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.card-editor-slick-swatch--trigger {
  flex: 1;
}

.card-editor-slick-create {
  border-style: dashed;
}

.card-editor-slick-empty {
  margin: 0;
  padding: 0.45rem 0.55rem;
  color: var(--bo-ink-muted);
  font-size: 0.85rem;
}
</style>
