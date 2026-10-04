type CardFieldDefinitionBase = {
  fieldCode: string;
  displayName: string;
  emoji: string | null;
  nullable: boolean;
  showOnCard: boolean;
};

export type CardFieldDefinition = CardFieldDefinitionBase & (
  | { type: 'text'; defaultValue: string | null; minLength: number | null; maxLength: number | null }
  | { type: 'integer'; defaultValue: number | null; min: number | null; max: number | null }
  | { type: 'date'; defaultValue: string | null }
);

// Unparseable numeric input is sent unchanged so the API can report the error.
// Successful responses always use CardFieldDefinition.
export type CardFieldDefinitionInput = CardFieldDefinitionBase & (
  | { type: 'text'; defaultValue: string | null; minLength: number | string | null; maxLength: number | string | null }
  | { type: 'integer'; defaultValue: number | string | null; min: number | string | null; max: number | string | null }
  | { type: 'date'; defaultValue: string | null }
);

export type CardFieldValues = Record<string, string | number | null>;
