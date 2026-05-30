<template>
  <section class="entity-rows-page">
    <header class="entity-rows-header">
      <h2>Tags</h2>
      <button type="button" class="btn" :disabled="busy" aria-label="Add tag" title="Add tag" @click="openCreateEditor">
        <Plus :size="16" aria-hidden="true" />
        <span>Add Tag</span>
      </button>
    </header>

    <p v-if="tagNames.length === 0" class="entity-rows-empty">No tags yet. Add one to get started.</p>

    <section v-else class="entity-rows-list">
      <article v-for="tagName in tagNames" :key="tagName" class="entity-row">
        <button
          type="button"
          class="entity-row-main entity-row-main-button"
          :disabled="busy"
          :aria-label="`Edit tag ${tagName}`"
          @click="openEditor(tagName)"
        >
          <span class="entity-row-title">{{ tagName }}</span>
          <span class="entity-row-badges tag-group">
            <Tag :tag-name="tagName" />
          </span>
        </button>
        <div class="entity-row-actions">
          <button
            type="button"
            class="btn btn--secondary entity-row-action-icon"
            :disabled="busy"
            aria-label="Edit tag"
            title="Edit tag"
            @click="openEditor(tagName)"
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
import Tag from '../components/Tag.vue';
import { useBoardStore } from '../stores/boardStore';
import { useTagStore } from '../stores/tagStore';

const router = useRouter();
const boardStore = useBoardStore();
const tagStore = useTagStore();
const { currentBoardId } = storeToRefs(boardStore);
const { tags, busy } = storeToRefs(tagStore);
const { loadTags, getTagByName } = tagStore;
const routeBoardId = computed(() => currentBoardId.value);

const tagNames = computed(() => tags.value.map(tag => tag.name).sort((left, right) => left.localeCompare(right)));

onMounted(() => {
  void initializeView();
});

async function openEditor(tagName: string) {
  const existingTag = getTagByName(tagName);
  if (!existingTag) {
    return;
  }

  if (routeBoardId.value === null) {
    return;
  }

  await router.push({ name: 'tags-tag', params: { boardId: routeBoardId.value, tagId: existingTag.id } });
}

async function openCreateEditor() {
  if (routeBoardId.value === null) {
    return;
  }

  await router.push({ name: 'tags-new', params: { boardId: routeBoardId.value } });
}

async function initializeView() {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    await router.replace({ name: 'boards' });
    return;
  }

  await loadTags(boardId);
}
</script>
