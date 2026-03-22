export type Card = {
  id: number;
  boardColumnId: number;
  title: string;
  description: string;
  sortKey: string;
  tagNames: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type TagStyleName = 'solid' | 'gradient';

export type Tag = {
  id: number;
  name: string;
  styleName: TagStyleName;
  stylePropertiesJson: string;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type Column = {
  id: number;
  title: string;
  sortKey: string;
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
