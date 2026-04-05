import { describe, expect, it } from 'vitest';
import { createCardSearchAndTagMatcher } from './cardFilters';

describe('createCardSearchAndTagMatcher', () => {
  it('matches all cards when search and tag filters are empty', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '   ',
      includedTagNames: [],
      excludedTagNames: []
    });

    expect(matcher(makeCard({ title: 'Ship feature', description: 'Ready for release', tagNames: [] }))).toBe(true);
  });

  it('matches search text against title and description case-insensitively', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '  release notes  ',
      includedTagNames: [],
      excludedTagNames: []
    });

    expect(matcher(makeCard({ title: 'Ship feature', description: 'Prepare RELEASE notes', tagNames: [] }))).toBe(true);
    expect(matcher(makeCard({ title: 'Backlog tidy-up', description: 'No related content', tagNames: [] }))).toBe(false);
  });

  it('includes a card when it has any included tag', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: ['Urgent', 'Bug'],
      excludedTagNames: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['bug'] }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['chore'] }))).toBe(false);
  });

  it('excludes a card when it has any excluded tag', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: [],
      excludedTagNames: ['blocked', 'wip']
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['Blocked'] }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['review'] }))).toBe(true);
  });

  it('applies include and exclude filters together', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: ['Feature', 'Bug'],
      excludedTagNames: ['Archived']
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['Feature'] }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['Feature', 'Archived'] }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task C', description: '', tagNames: ['Chore'] }))).toBe(false);
  });
});

function makeCard(overrides: { title: string; description: string; tagNames: string[] }) {
  return {
    id: 1,
    boardColumnId: 10,
    cardTypeId: 1,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title: overrides.title,
    description: overrides.description,
    sortKey: '00000000000000000010',
    tagNames: overrides.tagNames,
    createdAtUtc: '2026-04-03T00:00:00Z',
    updatedAtUtc: '2026-04-03T00:00:00Z'
  };
}
