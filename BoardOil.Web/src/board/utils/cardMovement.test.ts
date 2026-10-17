import { describe, expect, it } from 'vitest';
import { findMovedColumns } from './cardMovement';

describe('findMovedColumns', () => {
  it('ignores duplicate delivery of the same ordering', () => {
    expect([...findMovedColumns({ 1: [1, 2], 2: [3] }, { 1: [1, 2], 2: [3] })]).toEqual([]);
  });
  it('ignores index changes caused solely by creation or removal', () => {
    expect([...findMovedColumns({ 1: [1, 2, 3] }, { 1: [4, 2, 3] })]).toEqual([]);
  });
  it('finds reordering within a column', () => {
    expect([...findMovedColumns({ 1: [1, 2, 3] }, { 1: [3, 1, 2] })]).toEqual([1]);
  });
  it('includes both columns when the flattened card order is unchanged', () => {
    expect([...findMovedColumns({ 1: [1, 2], 2: [3] }, { 1: [1], 2: [2, 3] })]).toEqual([1, 2]);
  });
  it('handles moving the last card into an empty column', () => {
    expect([...findMovedColumns({ 1: [1] }, { 2: [1] })]).toEqual([1, 2]);
  });
  it('finds a move alongside simultaneous creation and removal', () => {
    expect([...findMovedColumns({ 1: [1, 2], 2: [3] }, { 1: [4], 2: [2, 3] })]).toEqual([1, 2]);
  });
});
