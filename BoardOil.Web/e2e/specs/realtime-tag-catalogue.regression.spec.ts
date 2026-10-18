import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

for (const operation of ['create', 'update', 'bulk edit']) {
  test(`a new tag introduced by remote card ${operation} appears without reloading`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Realtime tag catalogue ${operation}`);
    let card = await api.createCard(board, 'Todo', 'Existing card');
    const bp = new BoardPage(page);
    const initialCatalogues = Promise.all(['tags', 'card-types', 'slicks'].map(catalogue =>
      page.waitForResponse(response => response.url().endsWith(`/api/boards/${board.id}/${catalogue}`)
        && response.request().method() === 'GET')
    ));
    await bp.open(board.id);
    await initialCatalogues;
    await expect(bp.card('Todo', card.title)).toBeVisible();
    const unrelatedRefreshes: string[] = [];
    const unrelatedPaths = new Set([
      `/api/boards/${board.id}`,
      `/api/boards/${board.id}/card-types`,
      `/api/boards/${board.id}/slicks`
    ]);
    page.on('request', request => {
      const path = new URL(request.url()).pathname;
      if (request.method() === 'GET' && unrelatedPaths.has(path)) {
        unrelatedRefreshes.push(path);
      }
    });
    const tagName = 'New remote tag';
    const catalogueRefresh = page.waitForResponse(response =>
      response.url().endsWith(`/api/boards/${board.id}/tags`) && response.request().method() === 'GET'
    );

    if (operation === 'create') {
      card = await api.createCard(board, 'Todo', 'New card', '', [tagName]);
    } else if (operation === 'update') {
      card = await api.updateCard(board.id, card, { tagNames: [tagName] });
    } else {
      await api.addTagsToCards(board.id, [card.id], [tagName]);
    }

    expect((await catalogueRefresh).ok()).toBe(true);
    await expect(bp.card('Todo', card.title).getByLabel(tagName, { exact: true })).toBeVisible();
    await bp.openCardFilters();
    await expect(page.getByRole('button', { name: `Move to include ${tagName}`, exact: true })).toBeVisible();
    expect(unrelatedRefreshes).toEqual([]);
  });
}
