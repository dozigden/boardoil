import { describe, expect, it } from 'vitest';
import { demoFieldDefinitions, planDemoFields, previewDemoTypeChange } from './demoCustomFields';

describe('demo field updates', () => {
  const original = { Reference: 'Old', Points: 0, Due: null };
  it('normalises values and preserves explicit clears', () => {
    expect(planDemoFields(demoFieldDefinitions, original, { Reference: ' New ', Points: 5, Due: '2026-10-04' }, demoFieldDefinitions, false, ''))
      .toEqual({ ok: true, data: { customFieldValues: { Reference: 'New', Points: 5, Due: '2026-10-04' }, description: '', losses: [] } });
  });
  it.each([
    [{ Reference: null, Points: 101, Due: null }, 'customFieldValues.Points'],
    [{ Reference: null, Points: 0, Due: '2026-02-30' }, 'customFieldValues.Due'],
    [{ Reference: null, Points: 0 }, 'customFieldValues.Due'],
    [{ ...original, Other: 'unknown' }, 'customFieldValues.Other']
  ])('rejects invalid or incomplete source values before converting', (fields, path) => {
    const result = planDemoFields(demoFieldDefinitions, original, fields, [], true, '');
    expect(result.ok).toBe(false);
    if (!result.ok) expect(result.error.validationErrors?.[path]).toBeDefined();
    expect(original.Reference).toBe('Old');
  });
  it('converts edited source values and records the edited losses', () => {
    const result = planDemoFields(demoFieldDefinitions, original, { Reference: 'Edited', Points: 7, Due: null },
      [{ dataType: 'text', code: 'Effort', name: 'Estimate', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: null }], true, 'Description');
    expect(result).toEqual({ ok: true, data: { customFieldValues: { Effort: '7' }, description: 'Description', losses: [{ name: 'Reference', value: 'Edited' }] } });
  });
});

describe('demo type-change previews', () => {
  const source = { name: 'Source', customFields: demoFieldDefinitions };
  const original = { Reference: 'Saved', Points: 0, Due: null };
  it('uses edited values, aligns conversions and identifies populated losses without mutation', () => {
    const target = { name: 'Target', customFields: [
      { ...demoFieldDefinitions[0]!, code: 'Effort', name: 'Estimate' }
    ] };
    const result = previewDemoTypeChange(source, original, { ...original, Reference: ' Edited ', Points: 7 }, target, true);
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.data.requiresConfirmation).toBe(true);
    expect(result.data.hasLosses).toBe(true);
    expect(result.data.rows[0]).toMatchObject({ original: { value: 'Edited' }, destination: null, losesValue: true });
    expect(result.data.rows[1]).toMatchObject({ original: { dataType: 'integer', value: 7 }, destination: { dataType: 'text', value: '7' }, losesValue: false });
    expect(result.data.rows[2]).toMatchObject({ original: { value: null }, destination: null, losesValue: false });
    expect(original.Reference).toBe('Saved');
  });
  it('skips confirmation for equivalent fields ignoring codes, casing and ordering', () => {
    const target = { name: 'Target', customFields: demoFieldDefinitions.map(field => ({
      ...field, code: `New${field.code}`, name: field.name.toUpperCase()
    })).reverse() };
    const result = previewDemoTypeChange(source, original, undefined, target, true);
    expect(result).toMatchObject({ ok: true, data: { requiresConfirmation: false, hasLosses: false } });
  });
});
