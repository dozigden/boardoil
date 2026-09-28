#!/usr/bin/env node

import { readFile, writeFile } from 'node:fs/promises';
import { execFile } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const fixturePath = path.join(repositoryRoot, 'BoardOil.Web/src/demo/data/public-demo-board.json');
const [exportPath, outputPath] = process.argv.slice(2);

if (!exportPath || !outputPath) {
  console.error('Usage: node scripts/refresh-demo-board.mjs <board.json|export.zip> <candidate-output.json>');
  process.exitCode = 1;
} else {
  if (path.resolve(outputPath) === fixturePath) {
    throw new Error('Write a candidate file first so the public fixture can be reviewed before replacement.');
  }
  const [source, fixture] = await Promise.all([
    readExport(exportPath),
    readFile(fixturePath, 'utf8').then(JSON.parse)
  ]);
  const sourceCards = new Map(source.columns.flatMap(column => column.cards.map(card => [card.id, card])));
  const seenIds = new Set();

  for (const column of fixture.columns) {
    for (const card of column.cards) {
      if (seenIds.has(card.id)) {
        throw new Error(`Duplicate public demo card #${card.id}`);
      }
      seenIds.add(card.id);

      const exported = sourceCards.get(card.id);
      if (!exported) {
        if (card.image) { continue; }
        throw new Error(`Public demo card #${card.id} is absent from the export`);
      }

      // Public titles, descriptions, image placement, column placement and board copy stay editorial.
      // Exported comments, email addresses, external URLs and internal notes never enter the fixture.
      card.cardTypeName = exported.cardTypeName;
      card.tagNames = exported.tagNames;
      card.slickName = exported.slickName;
      card.cardCreatedUtc = exported.cardCreatedUtc;
      card.cardUpdatedUtc = exported.cardUpdatedUtc;
    }
  }

  for (const catalogue of ['cardTypes', 'tags', 'slicks']) {
    const exportedByName = new Map(source[catalogue].map(item => [item.name, item]));
    fixture[catalogue] = fixture[catalogue].map(item => {
      const exported = exportedByName.get(item.name);
      if (!exported) {
        throw new Error(`Public demo ${catalogue} entry ${item.name} is absent from the export`);
      }
      if (catalogue === 'cardTypes') {
        return { ...item, emoji: exported.emoji, isSystem: exported.isSystem };
      }
      if (catalogue === 'tags') {
        return { ...item, emoji: exported.emoji };
      }
      return item;
    });
  }

  await writeFile(outputPath, `${JSON.stringify(fixture, null, 2)}\n`);
  console.log(`Wrote ${seenIds.size} public demo cards to ${outputPath}`);
}

async function readExport(exportPath) {
  if (path.extname(exportPath).toLowerCase() === '.zip') {
    const { stdout } = await promisify(execFile)('unzip', ['-p', exportPath, 'board.json'], {
      maxBuffer: 16 * 1024 * 1024
    });
    return JSON.parse(stdout);
  }
  return JSON.parse(await readFile(exportPath, 'utf8'));
}
