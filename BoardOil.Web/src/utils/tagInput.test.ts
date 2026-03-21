import { describe, expect, it } from 'vitest';
import { mergeTagNames, parseTagInputValues } from './tagInput';

describe('tagInput', () => {
  it('parses comma-separated values and trims whitespace', () => {
    const result = parseTagInputValues(['Bug, Needs Triage', '  Sprint 1  ']);
    expect(result).toEqual(['Bug', 'Needs Triage', 'Sprint 1']);
  });

  it('dedupes parsed tags case-insensitively while preserving first casing', () => {
    const result = parseTagInputValues(['Bug', ' bug ', 'BUG']);
    expect(result).toEqual(['Bug']);
  });

  it('merges existing and added tags without duplicates', () => {
    const merged = mergeTagNames(['Bug'], ['urgent', ' bug ', 'Needs Triage']);
    expect(merged).toEqual(['Bug', 'urgent', 'Needs Triage']);
  });
});
