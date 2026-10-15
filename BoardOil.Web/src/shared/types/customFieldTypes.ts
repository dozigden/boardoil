type CustomFieldDefinitionBase = {
  code: string;
  name: string;
  emoji: string | null;
  nullable: boolean;
  showOnCard: boolean;
};

export type CustomFieldDefinition = CustomFieldDefinitionBase & (
  | { dataType: 'text'; defaultValue: string | null; minLength: number | null; maxLength: number | null }
  | { dataType: 'integer'; defaultValue: number | null; min: number | null; max: number | null }
  | { dataType: 'date'; defaultValue: string | null }
);

// Unparseable numeric input is sent unchanged so the API can report the error.
// Successful responses always use CustomFieldDefinition.
export type CustomFieldDefinitionInput = CustomFieldDefinitionBase & (
  | { dataType: 'text'; defaultValue: string | null; minLength: number | string | null; maxLength: number | string | null }
  | { dataType: 'integer'; defaultValue: number | string | null; min: number | string | null; max: number | string | null }
  | { dataType: 'date'; defaultValue: string | null }
);

export type CustomFieldValues = Record<string, string | number | null>;

export type CustomFieldApplicability = { mode: 'all' } | { mode: 'selected'; cardTypeIds: number[] };
export type CustomField = { id: number; definition: CustomFieldDefinition; appliesTo: CustomFieldApplicability };
export type CustomFieldInput = { definition: CustomFieldDefinitionInput; appliesTo: CustomFieldApplicability };
export type CustomFieldPreview = { affectedCardCount: number; removedValueCount: number; replacedValueCount: number };

export type CardTypeChangeValue = {
  name: string;
  emoji: string | null;
  dataType: CustomFieldDefinition['dataType'];
  value: string | number | null;
};
export type CardTypeChangeRow = {
  original: CardTypeChangeValue | null;
  destination: CardTypeChangeValue | null;
  losesValue: boolean;
};
export type CardTypeChangePreview = {
  originalTypeName: string;
  destinationTypeName: string;
  requiresConfirmation: boolean;
  hasLosses: boolean;
  rows: CardTypeChangeRow[];
};
