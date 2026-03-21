<template>
  <section class="tags-manager">
    <header class="tags-header">
      <h2>Tag Styles</h2>
      <button type="button" class="ghost tags-refresh" @click="loadTags">
        Refresh
      </button>
    </header>
    <p class="tags-hint">Tag names are shared globally. Editing style here updates all cards using that tag.</p>

    <p v-if="tagNames.length === 0" class="tags-empty">No tags yet. Add one from any card editor.</p>

    <article v-for="tagName in tagNames" :key="tagName" class="tags-item">
      <div class="tags-item-header">
        <h3>{{ tagName }}</h3>
        <span class="tag-pill" :style="previewStyle(tagName)">
          {{ tagName }}
        </span>
      </div>

      <label>
        Style
        <select :value="draftFor(tagName).styleName" @change="setStyleName(tagName, ($event.target as HTMLSelectElement).value)">
          <option value="solid">Solid</option>
          <option value="gradient">Gradient</option>
        </select>
      </label>

      <template v-if="draftFor(tagName).styleName === 'solid'">
        <label>
          Background Color
          <input
            :value="draftFor(tagName).backgroundColor"
            maxlength="7"
            placeholder="#RRGGBB"
            @input="setDraftField(tagName, 'backgroundColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>

      <template v-else>
        <label>
          Left Color
          <input
            :value="draftFor(tagName).leftColor"
            maxlength="7"
            placeholder="#RRGGBB"
            @input="setDraftField(tagName, 'leftColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
        <label>
          Right Color
          <input
            :value="draftFor(tagName).rightColor"
            maxlength="7"
            placeholder="#RRGGBB"
            @input="setDraftField(tagName, 'rightColor', ($event.target as HTMLInputElement).value)"
          />
        </label>
      </template>

      <label>
        Text Color Mode
        <select :value="draftFor(tagName).textColorMode" @change="setTextMode(tagName, ($event.target as HTMLSelectElement).value)">
          <option value="auto">Auto Contrast</option>
          <option value="custom">Custom</option>
        </select>
      </label>

      <label v-if="draftFor(tagName).textColorMode === 'custom'">
        Text Color
        <input
          :value="draftFor(tagName).textColor"
          maxlength="7"
          placeholder="#RRGGBB"
          @input="setDraftField(tagName, 'textColor', ($event.target as HTMLInputElement).value)"
        />
      </label>

      <div class="tags-item-actions">
        <button type="button" class="tags-save" :disabled="busy" @click="saveTag(tagName)">
          Save Style
        </button>
      </div>
    </article>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref, watch } from 'vue';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';
import { DEFAULT_TAG_STYLE_PROPERTIES_JSON, buildStylePropertiesJsonFromDraft, createTagStyleDraft, getTagPillStyle } from '../utils/tagStyles';
import type { TagStyleDraft } from '../utils/tagStyles';
import type { Tag, TagStyleName } from '../types/boardTypes';

const boardStore = useBoardStore();
const tagStore = useTagStore();
const { board } = storeToRefs(boardStore);
const { tags, busy } = storeToRefs(tagStore);
const { loadTags, updateTagStyle, getTagByName } = tagStore;

const drafts = ref<Record<string, TagStyleDraft>>({});

const tagNames = computed(() => {
  const seenByNormalisedName = new Map<string, string>();
  for (const tag of tags.value) {
    seenByNormalisedName.set(normaliseTagNameKey(tag.name), tag.name);
  }

  if (board.value) {
    for (const column of board.value.columns) {
      for (const card of column.cards) {
        for (const rawTagName of card.tagNames) {
          const tagName = rawTagName.trim();
          if (!tagName) {
            continue;
          }

          const key = normaliseTagNameKey(tagName);
          if (!seenByNormalisedName.has(key)) {
            seenByNormalisedName.set(key, tagName);
          }
        }
      }
    }
  }

  return [...seenByNormalisedName.values()].sort((left, right) => left.localeCompare(right));
});

onMounted(async () => {
  await loadTags();
});

watch(
  [tagNames, tags],
  ([nextTagNames]) => {
    const nextDrafts: Record<string, TagStyleDraft> = {};
    for (const tagName of nextTagNames) {
      nextDrafts[tagName] = createTagStyleDraft(resolveTag(tagName));
    }

    drafts.value = nextDrafts;
  },
  { immediate: true }
);

function draftFor(tagName: string): TagStyleDraft {
  return drafts.value[tagName];
}

function setStyleName(tagName: string, value: string) {
  const styleName: TagStyleName = value === 'gradient' ? 'gradient' : 'solid';
  setDraft(tagName, {
    ...draftFor(tagName),
    styleName
  });
}

function setTextMode(tagName: string, value: string) {
  const textColorMode = value === 'custom' ? 'custom' : 'auto';
  setDraft(tagName, {
    ...draftFor(tagName),
    textColorMode
  });
}

function setDraftField(tagName: string, field: keyof TagStyleDraft, value: string) {
  setDraft(tagName, {
    ...draftFor(tagName),
    [field]: value
  });
}

function setDraft(tagName: string, draft: TagStyleDraft) {
  drafts.value = {
    ...drafts.value,
    [tagName]: draft
  };
}

function previewStyle(tagName: string) {
  const draft = draftFor(tagName);
  return getTagPillStyle({
    name: tagName,
    styleName: draft.styleName,
    stylePropertiesJson: buildStylePropertiesJsonFromDraft(draft),
    createdAtUtc: '',
    updatedAtUtc: ''
  });
}

async function saveTag(tagName: string) {
  const draft = draftFor(tagName);
  await updateTagStyle(tagName, draft.styleName, buildStylePropertiesJsonFromDraft(draft));
}

function resolveTag(tagName: string): Tag {
  return getTagByName(tagName) ?? {
    name: tagName,
    styleName: 'solid',
    stylePropertiesJson: DEFAULT_TAG_STYLE_PROPERTIES_JSON,
    createdAtUtc: '',
    updatedAtUtc: ''
  };
}

function normaliseTagNameKey(tagName: string) {
  return tagName.trim().toUpperCase();
}
</script>
