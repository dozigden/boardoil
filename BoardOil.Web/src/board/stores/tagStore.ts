import { defineStore } from 'pinia';
import { ref } from 'vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { StyleDefault, Tag, TagCreateModel, TagEditModel } from '../../shared/types/boardTypes';
import type { AppError } from '../../shared/types/appError';
import type { Result } from '../../shared/types/result';

export const useTagStore = defineStore('tag', () => {
  const tags = ref<Tag[]>([]);
  const busy = ref(false);
  const activeBoardId = ref<number | null>(null);
  const feedback = useUiFeedbackStore();
  const api = createBoardApi();

  function SET_TAGS(boardId: number, nextTags: Tag[]) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    tags.value = sortTags(nextTags);
  }

  function UPSERT_TAG(boardId: number, tag: Tag) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    const existingIndex = tags.value.findIndex(x => x.id === tag.id || x.name === tag.name);
    if (existingIndex < 0) {
      tags.value = sortTags([...tags.value, tag]);
      return;
    }

    const next = [...tags.value];
    next[existingIndex] = tag;
    tags.value = sortTags(next);
  }

  function REMOVE_TAG(boardId: number, tagId: number) {
    if (activeBoardId.value !== boardId) {
      return;
    }

    tags.value = tags.value.filter(tag => tag.id !== tagId);
  }

  function dispose() {
    activeBoardId.value = null;
    tags.value = [];
    busy.value = false;
  }

  async function loadTags(boardId: number) {
    if (activeBoardId.value !== boardId) {
      tags.value = [];
      busy.value = false;
    }

    activeBoardId.value = boardId;
    const result = await api.getTags(boardId);
    if (activeBoardId.value !== boardId) {
      return false;
    }

    if (!result.ok) {
      reportError(result.error);
      return false;
    }

    SET_TAGS(boardId, result.data);
    feedback.clearError('tag');
    return true;
  }

  async function createTag(boardId: number, model: TagCreateModel) {
    const result = await runApiOperation(boardId, () => api.createTag(boardId, model), { suppressError: true });
    if (result.ok) UPSERT_TAG(boardId, result.data);
    return result;
  }

  async function getCreateDefaultStyle(boardId: number): Promise<StyleDefault | null> {
    const result = await runApiOperation(boardId, () => api.getTagCreateDefaultStyle(boardId));
    if (!result.ok) {
      return null;
    }

    return result.data;
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

      const created = await createTag(boardId, { name: trimmedTagName });
      if (created.ok) {
        resolvedTagNames.push(created.data.name);
      } else if (activeBoardId.value === boardId) {
        reportError(created.error);
      }
    }

    return dedupeTagNames(resolvedTagNames);
  }

  async function updateTagStyle(
    boardId: number,
    tagId: number,
    model: TagEditModel
  ) {
    const result = await runApiOperation(boardId, () => api.updateTagStyle(boardId, tagId, model), { suppressError: true });
    if (result.ok) UPSERT_TAG(boardId, result.data);
    return result;
  }

  async function saveTag(
    boardId: number,
    tagId: number | null,
    model: TagEditModel
  ) {
    if (tagId === null) {
      return createTag(boardId, model);
    }

    return updateTagStyle(boardId, tagId, model);
  }

  async function deleteTag(boardId: number, tagId: number) {
    const result = await runApiOperation(boardId, () => api.deleteTag(boardId, tagId), { suppressError: true });
    if (result.ok) REMOVE_TAG(boardId, tagId);
    return result;
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

  async function runApiOperation<T>(boardId: number, operation: () => Promise<Result<T, AppError>>, options: { suppressError?: boolean } = {}) {
    if (activeBoardId.value === boardId) {
      busy.value = true;
    }
    try {
      const result = await operation();
      if (activeBoardId.value !== boardId) {
        return result;
      }

      if (!result.ok) {
        if (!options.suppressError) reportError(result.error);
      } else {
        feedback.clearError('tag');
      }

      return result;
    } finally {
      if (activeBoardId.value === boardId) {
        busy.value = false;
      }
    }
  }

  function reportError(error: AppError) {
    feedback.setError(error.message, 'tag');
  }

  return {
    tags,
    busy,
    activeBoardId,
    dispose,
    loadTags,
    getCreateDefaultStyle,
    createTag,
    ensureTagsExist,
    updateTagStyle,
    saveTag,
    deleteTag,
    getTagById,
    getTagByName
  };
});

function sortTags(tags: Tag[]) {
  return [...tags].sort((left, right) => left.name.localeCompare(right.name));
}

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
