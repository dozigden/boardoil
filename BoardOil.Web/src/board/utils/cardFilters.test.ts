import { describe, expect, it } from 'vitest';
import { createCardSearchAndTagMatcher } from './cardFilters';

describe('createCardSearchAndTagMatcher', () => {
  it('matches all cards when search and tag filters are empty', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '   ',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Ship feature', description: 'Ready for release', tagNames: [] }))).toBe(true);
  });

  it('matches search text against title and description case-insensitively', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '  release notes  ',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Ship feature', description: 'Prepare RELEASE notes', tagNames: [] }))).toBe(true);
    expect(matcher(makeCard({ title: 'Backlog tidy-up', description: 'No related content', tagNames: [] }))).toBe(false);
  });

  it('matches search text against external URL case-insensitively', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: 'GITHUB.COM/example',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({
      title: 'Repository',
      description: '',
      externalUrl: 'https://github.com/Example/project',
      tagNames: []
    }))).toBe(true);
    expect(matcher(makeCard({ title: 'Other', description: '', tagNames: [] }))).toBe(false);
  });

  it('includes a card when it has any included tag', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: ['Urgent', 'Bug'],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['bug'] }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['chore'] }))).toBe(false);
  });

  it('excludes a card when it has any excluded tag', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: [],
      excludedTagNames: ['blocked', 'wip'],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['Blocked'] }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['review'] }))).toBe(true);
  });

  it('applies include and exclude filters together', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: ['Feature', 'Bug'],
      excludedTagNames: ['Archived'],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['Feature'] }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['Feature', 'Archived'] }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task C', description: '', tagNames: ['Chore'] }))).toBe(false);
  });

  it('includes cards by slick when any included slick matches', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: ['Delivery'],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: [], slickName: 'delivery' }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: [], slickName: 'Planning' }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task C', description: '', tagNames: [] }))).toBe(false);
  });

  it('excludes cards when an excluded slick matches', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: ['Blocked'],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: [], slickName: 'Blocked' }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: [], slickName: 'Delivery' }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task C', description: '', tagNames: [] }))).toBe(true);
  });

  it('applies tag and slick filters together', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: ['Feature'],
      excludedTagNames: [],
      includedSlickNames: ['Alpha'],
      excludedSlickNames: ['Beta'],
      includedCardTypeIds: [],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: ['Feature'], slickName: 'Alpha' }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: ['Feature'], slickName: 'Beta' }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task C', description: '', tagNames: ['Feature'], slickName: 'Gamma' }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task D', description: '', tagNames: ['Chore'], slickName: 'Alpha' }))).toBe(false);
  });

  it('includes cards by card type id when any included card type matches', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [2, 3],
      excludedCardTypeIds: []
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: [], cardTypeId: 2 }))).toBe(true);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: [], cardTypeId: 4 }))).toBe(false);
  });

  it('excludes cards by card type id when an excluded card type matches', () => {
    const matcher = createCardSearchAndTagMatcher({
      searchText: '',
      includedTagNames: [],
      excludedTagNames: [],
      includedSlickNames: [],
      excludedSlickNames: [],
      includedCardTypeIds: [],
      excludedCardTypeIds: [2]
    });

    expect(matcher(makeCard({ title: 'Task A', description: '', tagNames: [], cardTypeId: 2 }))).toBe(false);
    expect(matcher(makeCard({ title: 'Task B', description: '', tagNames: [], cardTypeId: 4 }))).toBe(true);
  });
});

function makeCard(overrides: {
  title: string;
  description: string;
  externalUrl?: string | null;
  tagNames: string[];
  slickName?: string | null;
  cardTypeId?: number;
}) {
  return {
    id: 1,
    boardColumnId: 10,
    cardTypeId: overrides.cardTypeId ?? 1,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title: overrides.title,
    description: overrides.description,
    externalUrl: overrides.externalUrl ?? null,
    slickName: overrides.slickName ?? null,
    sortKey: '00000000000000000010',
    tags: overrides.tagNames.map((name, index) => ({
      id: index + 1,
      name,
      styleName: 'solid' as const,
      stylePropertiesJson: '{"backgroundColor":"#224466","textColorMode":"auto"}',
      emoji: null
    })),
    tagNames: overrides.tagNames,
    cardCreatedUtc: '2026-04-03T00:00:00Z',
    cardUpdatedUtc: '2026-04-03T00:00:00Z'
  };
}
