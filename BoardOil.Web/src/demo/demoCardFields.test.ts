import { describe, expect, it } from 'vitest';
import { demoFieldDefinitions, planDemoFields } from './demoCardFields';

describe('demo field updates', () => {
  const original = { Reference: 'Old', Points: 0, Due: null };
  it('normalises values and preserves explicit clears', () => {
    expect(planDemoFields(demoFieldDefinitions, original, { Reference: ' New ', Points: 5, Due: '2026-10-04' }, demoFieldDefinitions, false, ''))
      .toEqual({ ok: true, data: { fields: { Reference: 'New', Points: 5, Due: '2026-10-04' }, description: '' } });
  });
  it.each([
    [{ Reference: null, Points: 101, Due: null }, 'fields.Points'],
    [{ Reference: null, Points: 0, Due: '2026-02-30' }, 'fields.Due'],
    [{ Reference: null, Points: 0 }, 'fields.Due'],
    [{ ...original, Other: 'unknown' }, 'fields.Other']
  ])('rejects invalid or incomplete source values before converting', (fields, path) => {
    const result = planDemoFields(demoFieldDefinitions, original, fields, [], true, '');
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error.validationErrors?.[path]).toBeDefined();
    expect(original.Reference).toBe('Old');
  });
  it('converts edited source values and records the edited losses', () => {
    const result = planDemoFields(demoFieldDefinitions, original, { Reference: 'Edited', Points: 7, Due: null },
      [{ type: 'text', fieldCode: 'Effort', displayName: 'Estimate', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: null }], true, 'Description');
    expect(result).toEqual({ ok: true, data: { fields: { Effort: '7' }, description: 'Description\n\nFields removed during card type change:\n\n- Reference: Edited' } });
  });
});
