using BoardOil.Data.Abstractions.Entities;
using BoardOil.Data.Abstractions.Users;

namespace BoardOil.Services.Board.Import;

public sealed class ImportedUserResolver(IUserRepository userRepository)
{
    private readonly Dictionary<string, EntityUser?> _assigneeByNormalisedEmail = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntityUser?> _commentAuthorByNormalisedEmail = new(StringComparer.Ordinal);

    public void Reset()
    {
        _assigneeByNormalisedEmail.Clear();
        _commentAuthorByNormalisedEmail.Clear();
    }

    public async Task<EntityUser?> ResolveImportedAssignedUserAsync(string? assignedUserNormalisedEmail)
    {
        if (string.IsNullOrWhiteSpace(assignedUserNormalisedEmail))
        {
            return null;
        }

        if (_assigneeByNormalisedEmail.TryGetValue(assignedUserNormalisedEmail, out var cachedAssignee))
        {
            return cachedAssignee;
        }

        var user = await userRepository.GetByNormalisedEmailAsync(assignedUserNormalisedEmail);
        var resolvedAssignee = user is { IsActive: true } ? user : null;
        _assigneeByNormalisedEmail[assignedUserNormalisedEmail] = resolvedAssignee;
        return resolvedAssignee;
    }

    public async Task<EntityUser?> ResolveImportedCommentAuthorAsync(string? authorNormalisedEmail)
    {
        if (string.IsNullOrWhiteSpace(authorNormalisedEmail))
        {
            return null;
        }

        if (_commentAuthorByNormalisedEmail.TryGetValue(authorNormalisedEmail, out var cachedAuthor))
        {
            return cachedAuthor;
        }

        var user = await userRepository.GetByNormalisedEmailAsync(authorNormalisedEmail);
        _commentAuthorByNormalisedEmail[authorNormalisedEmail] = user;
        return user;
    }
}
