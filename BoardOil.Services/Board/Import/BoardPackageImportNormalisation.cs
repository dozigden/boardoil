using BoardOil.Services.Users;

namespace BoardOil.Services.Board.Import;

public static class BoardPackageImportNormalisation
{
    public static string ResolveImportedBoardName(string? requestBoardName, string? importedBoardName)
    {
        var sourceName = string.IsNullOrWhiteSpace(requestBoardName)
            ? importedBoardName
            : requestBoardName;

        return sourceName?.Trim() ?? string.Empty;
    }

    public static string ResolveImportedBoardDescription(string? importedBoardDescription) =>
        importedBoardDescription?.Trim() ?? string.Empty;

    public static string NormaliseTagName(string tagName) =>
        tagName.ToUpperInvariant();

    public static string NormaliseName(string value) =>
        value.ToUpperInvariant();

    public static string? ResolveNormalisedEmailOrNull(string? emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return null;
        }

        return EmailAddressRules.Validate(emailAddress, "email").Count > 0
            ? null
            : EmailAddressRules.TryNormalise(emailAddress);
    }

    public static string BuildArchiveSearchText(string title, IReadOnlyList<string> tagNames)
    {
        var values = new List<string> { NormaliseSearchValue(title) };
        values.AddRange(tagNames.Select(NormaliseSearchValue));
        return string.Join('\n', values.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string NormaliseSearchValue(string value) =>
        value.Trim().ToUpperInvariant();
}
