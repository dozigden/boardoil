using BoardOil.Contracts.CustomField;

namespace BoardOil.Services.CustomField;

public sealed record CustomFieldDraft(CustomFieldDefinition Definition, CustomFieldApplicability AppliesTo);
