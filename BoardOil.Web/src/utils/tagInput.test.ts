import { describe, expect, it } from 'vitest';
import { getTagCompletionQuery, getTagCompletionSuggestions, mergeTagNames, parseTagInputValues } from './tagInput';

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

  it('uses the last comma-delimited segment as the completion query', () => {
    expect(getTagCompletionQuery('Bug, Needs')).toBe('Needs');
    expect(getTagCompletionQuery('Bug,   ')).toBe('');
    expect(getTagCompletionQuery('Solo')).toBe('Solo');
  });

  it('filters completion suggestions case-insensitively and excludes selected tags', () => {
    const suggestions = getTagCompletionSuggestions(
      ['Bug', 'Bugfix', 'Documentation', 'urgent'],
      'FIX',
      ['Bug']
    );

    expect(suggestions).toEqual(['Bugfix']);
  });

  it('returns all available suggestions when the completion query is empty', () => {
    const suggestions = getTagCompletionSuggestions(
      ['Bug', 'Documentation', 'urgent'],
      '',
      ['documentation']
    );

    expect(suggestions).toEqual(['Bug', 'urgent']);
  });

  it('ranks starts-with matches before other contains matches', () => {
    const suggestions = getTagCompletionSuggestions(
      ['Pale', 'Alpine', 'Coral', 'Alpha'],
      'al',
      []
    );

    expect(suggestions).toEqual(['Alpine', 'Alpha', 'Pale', 'Coral']);
  });

  it('respects the suggestion limit after ranking', () => {
    const suggestions = getTagCompletionSuggestions(
      ['Pale', 'Alpine', 'Coral', 'Alpha'],
      'al',
      [],
      2
    );

    expect(suggestions).toEqual(['Alpine', 'Alpha']);
  });
});
