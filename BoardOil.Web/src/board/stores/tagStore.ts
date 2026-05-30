import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { Tag, TagEditModel } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useTagStore = defineStore('tag', () => {
  const tags = ref<Tag[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();
  let loadRequestVersion = 0;

  function dispose() {
    loadRequestVersion += 1;
    activeBoardId.value = null;
    tags.value = [];
    busy.value = false;
  }

  async function loadTags(boardId: number) {
    const requestVersion = ++loadRequestVersion;
    if (activeBoardId.value !== boardId) {
      tags.value = [];
    }

    activeBoardId.value = boardId;
    const result = await api.getTags(boardId);
    if (requestVersion !== loadRequestVersion) {
      return false;
    }

    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    tags.value = [...result.data].sort((a, b) => a.name.localeCompare(b.name));
    feedback.clearError();
    return true;
  }

  async function createTag(boardId: number, tagName: string, emoji?: string | null) {
    return createTagForBoard(boardId, tagName, emoji);
  }

  async function ensureTagsExist(boardId: number, tagNames: string[]) {
    const resolvedTagNames: string[] = [];
    for (const rawTagName of tagNames) {
      const trimmedTagName = rawTagName.trim();
      if (!trimmedTagName) {
        continue;
      }

      const existing = getTagByName(trimmedTagName);
      if (existing) {
        resolvedTagNames.push(existing.name);
        continue;
      }

      const created = await createTagForBoard(boardId, trimmedTagName);
      if (created) {
        resolvedTagNames.push(created.name);
      }
    }

    return dedupeTagNames(resolvedTagNames);
  }

  async function updateTagStyle(
    boardId: number,
    tagId: number,
    model: TagEditModel
  ) {
    return updateTagStyleForBoard(boardId, tagId, model);
  }

  async function saveTag(
    boardId: number,
    tagId: number | null,
    model: TagEditModel
  ) {
    if (tagId === null) {
      const createdTag = await createTagForBoard(boardId, model.name, model.emoji);
      if (!createdTag) {
        return null;
      }

      const styledTag = await updateTagStyleForBoard(boardId, createdTag.id, model);
      return { createdTag, savedTag: styledTag };
    }

    const updatedTag = await updateTagStyleForBoard(boardId, tagId, model);
    if (!updatedTag) {
      return null;
    }

    return { createdTag: null, savedTag: updatedTag };
  }

  async function deleteTag(boardId: number, tagId: number) {
    const result = await runBusy(() => api.deleteTag(boardId, tagId));
    if (!result.ok) {
      return false;
    }

    removeTag(tagId);
    return true;
  }

  function getTagById(tagId: number | null) {
    if (tagId === null) {
      return null;
    }

    return tags.value.find(x => x.id === tagId) ?? null;
  }

  function getTagByName(tagName: string | null) {
    if (tagName === null) {
      return null;
    }

    return tags.value.find(x => x.name === tagName)
      ?? tags.value.find(x => x.name.toLowerCase() === tagName.toLowerCase())
      ?? null;
  }

  async function runBusy<T>(operation: () => Promise<Result<T, AppError>>) {
    busy.value = true;
    try {
      const result = await operation();
      if (!result.ok) {
        reportError(result.error);
      } else {
        feedback.clearError();
      }

      return result;
    } finally {
      busy.value = false;
    }
  }

  function upsertTag(tag: Tag) {
    const existingIndex = tags.value.findIndex(x => x.id === tag.id || x.name === tag.name);
    if (existingIndex < 0) {
      tags.value = [...tags.value, tag].sort((a, b) => a.name.localeCompare(b.name));
      return;
    }

    const next = [...tags.value];
    next[existingIndex] = tag;
    tags.value = next.sort((a, b) => a.name.localeCompare(b.name));
  }

  function removeTag(tagId: number) {
    tags.value = tags.value.filter(tag => tag.id !== tagId);
  }

  function reportError(error: AppError) {
    feedback.setError(error.message);
  }

  async function createTagForBoard(boardId: number, tagName: string, emoji?: string | null) {
    const result = await runBusy(() => api.createTag(boardId, tagName, emoji));
    if (!result.ok) {
      return null;
    }

    upsertTag(result.data);
    return result.data;
  }

  async function updateTagStyleForBoard(boardId: number, tagId: number, model: TagEditModel) {
    const result = await runBusy(() => api.updateTagStyle(boardId, tagId, model));
    if (!result.ok) {
      return null;
    }

    upsertTag(result.data);
    return result.data;
  }

  return {
    tags,
    busy,
    activeBoardId,
    dispose,
    loadTags,
    createTag,
    ensureTagsExist,
    updateTagStyle,
    saveTag,
    deleteTag,
    getTagById,
    getTagByName
  };
});

function dedupeTagNames(tagNames: string[]) {
  const deduped: string[] = [];
  const seen = new Set<string>();
  for (const tagName of tagNames) {
    const key = tagName.trim().toLowerCase();
    if (!key || seen.has(key)) {
      continue;
    }

    seen.add(key);
    deduped.push(tagName.trim());
  }

  return deduped;
}
