import { describe, expect, it } from 'vitest';
import { useEntityEditDraft } from './useEntityEditDraft';

type Source = {
  id: number;
  name: string;
  labels: string[];
};

type Draft = {
  name: string;
  labels: string[];
};

function cloneDraft(draft: Draft): Draft {
  return {
    ...draft,
    labels: [...draft.labels]
  };
}

function draftsEqual(left: Draft, right: Draft) {
  if (left.name !== right.name) {
    return false;
  }

  if (left.labels.length !== right.labels.length) {
    return false;
  }

  for (let index = 0; index < left.labels.length; index += 1) {
    if (left.labels[index] !== right.labels[index]) {
      return false;
    }
  }

  return true;
}

describe('useEntityEditDraft', () => {
  it('supports source init plus explicit user/system patch semantics', () => {
    const model = useEntityEditDraft<Source, Draft, number>({
      getId: source => source.id,
      toDraft: source => ({
        name: source.name,
        labels: [...source.labels]
      }),
      cloneDraft,
      areEqual: draftsEqual
    });

    expect(model.initFromSource({ id: 10, name: 'Alpha', labels: ['a'] })).toBe(true);
    expect(model.sourceId.value).toBe(10);
    expect(model.isDirty.value).toBe(false);

    model.patchFromSystem({ name: 'Alpha ' });
    expect(model.isDirty.value).toBe(false);

    model.patchFromUser({ name: 'Alpha user edit' });
    expect(model.isDirty.value).toBe(true);

    model.clear();
    expect(model.draft.value).toBeNull();
    expect(model.sourceId.value).toBeNull();
    expect(model.isDirty.value).toBe(false);
  });

  it('does not reinitialize when source id is unchanged', () => {
    const model = useEntityEditDraft<Source, Draft, number>({
      getId: source => source.id,
      toDraft: source => ({
        name: source.name,
        labels: [...source.labels]
      }),
      cloneDraft,
      areEqual: draftsEqual
    });

    expect(model.initFromSource({ id: 2, name: 'One', labels: [] })).toBe(true);
    expect(model.initFromSource({ id: 2, name: 'Two', labels: ['x'] })).toBe(false);
    expect(model.draft.value?.name).toBe('One');
  });
});
