import { describe, expect, it } from 'vitest';
import fixtures from '../../../test-data/card-checklist-counts.json';
import { getMarkdownChecklistCounts } from './markdownChecklistCounts';

describe('server checklist compatibility fixtures against the editor parser', () => {
  it.each(fixtures)('$name', ({ description, completed, total }) => {
    expect(getMarkdownChecklistCounts(description)).toEqual({ completed, total });
  });
});
