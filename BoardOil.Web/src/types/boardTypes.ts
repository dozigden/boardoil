export type Card = {
  id: number;
  boardColumnId: number;
  title: string;
  description: string;
  position: number;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type Column = {
  id: number;
  title: string;
  position: number;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type BoardColumn = Column & {
  cards: Card[];
};

export type Board = {
  id: number;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  columns: BoardColumn[];
};

export type ApiEnvelope<T> = {
  success: boolean;
  data: T | null;
  statusCode: number;
  message?: string;
};

export type TypingChangedEvent = {
  cardId: number;
  userLabel: string;
  isTyping: boolean;
  expiresAtUtc: string;
};
