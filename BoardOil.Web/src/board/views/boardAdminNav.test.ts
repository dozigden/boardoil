import { describe, expect, it } from 'vitest';
import { buildBoardAdminNavItems } from './boardAdminNav';

describe('buildBoardAdminNavItems', () => {
  it('returns owner navigation including card types management', () => {
    const items = buildBoardAdminNavItems(12, 'Owner');

    expect(items.map(x => x.label)).toEqual(['Details', 'Columns', 'Card Types', 'Tags', 'Members', 'Delete board']);
  });

  it('returns contributor navigation without owner-only pages', () => {
    const items = buildBoardAdminNavItems(12, 'Contributor');

    expect(items.map(x => x.label)).toEqual(['Details', 'Tags']);
  });
});
