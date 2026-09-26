import { describe, expect, it } from 'vitest';
import { getMarkdownChecklistCounts } from './markdownChecklistCounts';

describe('getMarkdownChecklistCounts', () => {
  it.each(['', 'No tasks here', '- Ordinary list item', 'Inline [x] text'])('finds no tasks in %j', description => {
    expect(getMarkdownChecklistCounts(description)).toEqual({ completed: 0, total: 0 });
  });

  it('counts checked and unchecked tasks across lists and marker styles', () => {
    const description = '- [ ] Open\n- [x] Done\n\nOther list:\n\n* [X] Also done\n* [ ] Still open\n\n+ [x] Final task';
    expect(getMarkdownChecklistCounts(description)).toEqual({ completed: 3, total: 5 });
  });

  it('counts nested tasks independently from their parent', () => {
    const description = '- [x] Parent\n  - [ ] Child\n  - [x] Other child\n    - [ ] Grandchild';
    expect(getMarkdownChecklistCounts(description)).toEqual({ completed: 2, total: 4 });
  });

  it('uses the same bare checkbox normalization as the editor', () => {
    expect(getMarkdownChecklistCounts('[ ] Open\n[x] Done\n[X] Also done'))
      .toEqual({ completed: 2, total: 3 });
  });

  it('excludes fenced and inline code examples', () => {
    const description = [
      '```markdown', '- [x] Fenced task', '[ ] Bare fenced task', '```', '',
      '~~~', '- [ ] Another fenced task', '~~~', '',
      'Example: `- [x] Inline task`', '', '- [ ] Actual task'
    ].join('\n');
    expect(getMarkdownChecklistCounts(description)).toEqual({ completed: 0, total: 1 });
  });

  it('matches the editor treating a standalone indented checkbox as a task', () => {
    expect(getMarkdownChecklistCounts('    - [x] Indented task'))
      .toEqual({ completed: 1, total: 1 });
  });

  it('excludes checkbox text inside a parsed indented code block', () => {
    expect(getMarkdownChecklistCounts('    Example code\n    - [x] Code task'))
      .toEqual({ completed: 0, total: 0 });
  });

  it('counts task lists within blockquotes', () => {
    expect(getMarkdownChecklistCounts('> - [x] Done\n> - [ ] Open'))
      .toEqual({ completed: 1, total: 2 });
  });

  it('derives fresh counts when tasks are completed or removed', () => {
    expect(getMarkdownChecklistCounts('- [ ] Task')).toEqual({ completed: 0, total: 1 });
    expect(getMarkdownChecklistCounts('- [x] Task')).toEqual({ completed: 1, total: 1 });
    expect(getMarkdownChecklistCounts('')).toEqual({ completed: 0, total: 0 });
  });
});
