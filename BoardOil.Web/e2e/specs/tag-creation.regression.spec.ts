import { expect, test } from '../fixtures/boardOilTest';

test('tag creation retains a rejected draft and saves the complete tag on retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Complete tag creation');
  const styleUpdates: string[] = [];
  page.on('request', request => {
    if (request.method() === 'PUT' && request.url().includes(`/api/boards/${board.id}/tags/`)) {
      styleUpdates.push(request.url());
    }
  });

  const createUrl = `/boards/${board.id}/admin/tags/new`;
  await page.goto(createUrl);
  const dialog = page.getByRole('dialog', { name: 'Add Tag', exact: true });
  const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
  const style = dialog.getByRole('combobox', { name: 'Style', exact: true });
  const create = dialog.getByRole('button', { name: 'Create tag', exact: true });
  await name.fill('Release, urgent');
  await style.selectOption('solid');
  await dialog.getByLabel('Background Color', { exact: true }).fill('#123456');
  await dialog.getByRole('combobox', { name: 'Border', exact: true }).selectOption('none');
  await create.click();

  await expect(name).toHaveAttribute('aria-invalid', 'true');
  await expect(name).toBeFocused();
  await expect(dialog.getByRole('alert')).toContainText('Tag name must be a single value.');
  await expect(page).toHaveURL(createUrl);
  await expect(name).toHaveValue('Release, urgent');
  await expect(style).toHaveValue('solid');
  await expect(dialog.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');
  expect(await api.getTags(board.id)).toEqual([]);

  await name.fill('Release');
  await create.click();
  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(`/boards/${board.id}/admin/tags`);
  const tags = await api.getTags(board.id);
  expect(tags).toHaveLength(1);
  expect(tags[0]).toMatchObject({ name: 'Release', styleName: 'solid' });
  expect(JSON.parse(tags[0]!.stylePropertiesJson)).toMatchObject({ backgroundColor: '#123456', borderMode: 'none' });
  expect(styleUpdates).toEqual([]);

  await page.getByRole('button', { name: 'Edit tag Release', exact: true }).click();
  const editor = page.getByRole('dialog', { name: 'Edit Tag: Release', exact: true });
  await expect(editor.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');
  await expect(editor.getByRole('combobox', { name: 'Border', exact: true })).toHaveValue('none');
});
