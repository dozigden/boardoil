import { expect, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

test('system comments have no author or edit action in live and archived views', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('System comment presentation');
  const card = await api.createCard(board, 'Todo', 'Recorded field values');
  await api.addCardComment(board.id, card.id, 'Estimate: **7**');

  // System-comment generation is a later slice; exercise the read contract here.
  const systemMetadata = { isSystem: true, authorUserId: null, authorDisplayName: null, authorImageRelativePath: null };
  await page.route(`**/api/boards/${board.id}/cards/${card.id}/comments`, async route => {
    const response = await route.fetch();
    const body = await response.json();
    body.data = body.data.map((comment: object) => ({ ...comment, ...systemMetadata }));
    await route.fulfill({ response, json: body });
  });
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  const editor = new CardEditorPage(page);
  const comment = page.getByRole('article').filter({ hasText: 'Estimate: 7' });
  await expect(comment.getByLabel('System comment', { exact: true })).toHaveText('System');
  await expect(comment.getByText('Unknown user', { exact: true })).toHaveCount(0);
  await expect(comment.getByRole('img')).toHaveCount(0);
  await comment.getByRole('button', { name: 'Comment actions', exact: true }).click();
  await expect(comment.getByRole('button', { name: 'Edit', exact: true })).toHaveCount(0);
  await expect(comment.getByRole('button', { name: 'Delete', exact: true })).toBeVisible();
  await page.keyboard.press('Escape');
  await editor.requestCommentDeletion('Estimate: 7');
  const prompt = page.getByRole('dialog', { name: 'Delete comment', exact: true });
  await expect(prompt).toContainText('Delete this system comment?');
  await prompt.getByRole('button', { name: 'Cancel', exact: true }).click();

  await api.archiveCard(board.id, card.id);
  await page.route(`**/api/boards/${board.id}/cards/archived/${card.id}`, async route => {
    const response = await route.fetch();
    const body = await response.json();
    body.data.comments = body.data.comments.map((item: object) => ({ ...item, ...systemMetadata }));
    await route.fulfill({ response, json: body });
  });
  await page.goto(`/boards/${board.id}/archived/${card.id}`);
  const archivedComment = page.getByRole('article').filter({ hasText: 'Estimate: 7' });
  await expect(archivedComment.getByLabel('System comment', { exact: true })).toHaveText('System');
  await expect(archivedComment.getByText('Unknown user', { exact: true })).toHaveCount(0);
  await expect(archivedComment.getByRole('img')).toHaveCount(0);
  await expect(archivedComment.getByRole('button', { name: 'Comment actions', exact: true })).toHaveCount(0);
});
