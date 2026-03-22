<template>
  <section class="tags-manager">
    <header class="tags-header">
      <h2>Tag Styles</h2>
      <button type="button" class="ghost tags-refresh" @click="loadTags">
        Refresh
      </button>
    </header>
    <p class="tags-hint">Tag names are shared globally. Editing style updates all cards using that tag.</p>

    <p v-if="tagNames.length === 0" class="tags-empty">No tags yet. Add one from any card editor.</p>

    <section v-else class="tags-list">
      <article v-for="tagName in tagNames" :key="tagName" class="tags-item">
        <div class="tags-item-header">
          <div class="tags-item-meta">
            <h3>{{ tagName }}</h3>
            <span class="tag-pill" :style="tagStyle(tagName)">
              {{ tagName }}
            </span>
          </div>
          <button type="button" class="ghost tags-edit" :disabled="busy" @click="openEditor(tagName)">
            Edit
          </button>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';
import { DEFAULT_TAG_STYLE_PROPERTIES_JSON, getTagPillStyle } from '../utils/tagStyles';
import type { Tag } from '../types/boardTypes';

const router = useRouter();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { board } = storeToRefs(boardStore);
const { tags, busy } = storeToRefs(tagStore);
const { loadTags, getTagByName } = tagStore;

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

async function openEditor(tagName: string) {
  const existingTag = getTagByName(tagName);
  if (!existingTag) {
    return;
  }

  await router.push({ name: 'tags-tag', params: { tagId: existingTag.id } });
}

function tagStyle(tagName: string) {
  return getTagPillStyle(resolveTag(tagName));
}

function resolveTag(tagName: string | null): Tag | null {
  if (tagName === null) {
    return null;
  }

  return getTagByName(tagName) ?? {
    id: -1,
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
