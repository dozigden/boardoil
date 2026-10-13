import { expect, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

test('system comments have no author or edit action in live and archived views', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('System comment presentation');
  const literal = '![image](https://example.invalid/image.png)';
  await api.configureDefaultFields(board.id, [
    { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null, nullable: true,
      showOnCard: false, defaultValue: 7, min: null, max: null },
    { dataType: 'text', code: 'Reference', name: 'Reference', emoji: null, nullable: true,
      showOnCard: false, defaultValue: literal, minLength: null, maxLength: 250 }
  ]);
  const card = await api.createCard(board, 'Todo', 'Recorded field values');
  await api.configureDefaultFields(board.id, []);
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  const editor = new CardEditorPage(page);
  const comment = page.getByRole('article').filter({ hasText: 'Estimate: 7' });
  await expect(comment.getByLabel('System comment', { exact: true })).toHaveText('System');
  await expect(comment.getByText('Unknown user', { exact: true })).toHaveCount(0);
  await expect(comment.getByRole('img')).toHaveCount(0);
  await expect(comment.locator('pre code')).toContainText(literal);
  await comment.getByRole('button', { name: 'Comment actions', exact: true }).click();
  await expect(comment.getByRole('button', { name: 'Edit', exact: true })).toHaveCount(0);
  await expect(comment.getByRole('button', { name: 'Delete', exact: true })).toBeVisible();
  await page.keyboard.press('Escape');
  await editor.requestCommentDeletion('Estimate: 7');
  const prompt = page.getByRole('dialog', { name: 'Delete comment', exact: true });
  await expect(prompt).toContainText('Delete this system comment?');
  await prompt.getByRole('button', { name: 'Cancel', exact: true }).click();

  await api.archiveCard(board.id, card.id);
  await page.goto(`/boards/${board.id}/archived/${card.id}`);
  const archivedComment = page.getByRole('article').filter({ hasText: 'Estimate: 7' });
  await expect(archivedComment.getByLabel('System comment', { exact: true })).toHaveText('System');
  await expect(archivedComment.getByText('Unknown user', { exact: true })).toHaveCount(0);
  await expect(archivedComment.getByRole('img')).toHaveCount(0);
  await expect(archivedComment.getByRole('button', { name: 'Comment actions', exact: true })).toHaveCount(0);
});
