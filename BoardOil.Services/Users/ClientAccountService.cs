using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Abstractions.Users;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;
using BoardOil.Contracts.Users;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Persistence.Abstractions.Users;
using BoardOil.Services.Auth;

namespace BoardOil.Services.Users;

public sealed class ClientAccountService(
    IUserRepository userRepository,
    IPersonalAccessTokenRepository personalAccessTokenRepository,
    IPasswordHashService passwordHashService,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IClientAccountService
{
    private sealed record CreatedPat(EntityPersonalAccessToken Token, string PlainTextToken);

    public async Task<ApiResult<IReadOnlyList<ClientAccountDto>>> GetClientAccountsAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var users = (await userRepository.GetUsersOrderedAsync())
            .Where(x => x.IdentityType == UserIdentityType.Client)
            .Select(x => x.ToClientAccountDto())
            .ToList();

        return users;
    }

    public async Task<ApiResult<CreatedClientAccountDto>> CreateClientAccountAsync(CreateClientAccountRequest request)
    {
        using var scope = scopeFactory.Create();

        var validation = ValidateUserNameAndEmail(request.UserName, request.Email);
        if (validation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validation);
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var userName = request.UserName.Trim();
        var email = request.Email.Trim();
        var normalisedEmail = EmailAddressRules.TryNormalise(email)!;
        var exists = await userRepository.UserNameExistsAsync(userName);
        if (exists)
        {
            return ApiErrors.BadRequest("Username already exists.");
        }

        var emailExists = await userRepository.NormalisedEmailExistsAsync(normalisedEmail);
        if (emailExists)
        {
            return ApiErrors.BadRequest("Email already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new EntityUser
        {
            UserName = userName,
            Email = email,
            NormalisedEmail = normalisedEmail,
            PasswordHash = passwordHashService.HashPassword(CreateRandomPassword()),
            Role = role,
            IdentityType = UserIdentityType.Client,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var tokenName = string.IsNullOrWhiteSpace(request.TokenName) ? "Initial token" : request.TokenName.Trim();
        var tokenRequest = new CreateMachinePatRequest(
            tokenName,
            request.ExpiresInDays,
            request.Scopes);

        var patResult = BuildPatEntity(user, tokenRequest, now, MachinePatRules.DefaultClientScopes);
        if (!patResult.Success || patResult.Data is null)
        {
            return ApiResults.BadRequest<CreatedClientAccountDto>(patResult.Message ?? "Token creation failed.", patResult.ValidationErrors);
        }

        user.PersonalAccessTokens.Add(patResult.Data.Token);
        userRepository.Add(user);
        await scope.SaveChangesAsync();

        var createdToken = new CreatedMachinePatDto(
            MachinePatRules.ToMachinePatDto(patResult.Data.Token),
            patResult.Data.PlainTextToken);

        return ApiResults.Created(new CreatedClientAccountDto(user.ToClientAccountDto(), createdToken));
    }

    public async Task<ApiResult<ClientAccountDto>> UpdateClientAccountAsync(int clientAccountId, UpdateClientAccountRequest request)
    {
        using var scope = scopeFactory.Create();

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var emailValidation = EmailAddressRules.Validate(request.Email, "email");
        if (emailValidation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", emailValidation);
        }

        var user = userRepository.Get(clientAccountId);
        if (user is null || user.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        var normalisedEmail = EmailAddressRules.TryNormalise(request.Email)!;
        var emailExists = await userRepository.NormalisedEmailExistsForOtherUserAsync(user.Id, normalisedEmail);
        if (emailExists)
        {
            return ApiErrors.BadRequest("Email already exists.");
        }

        user.Email = request.Email.Trim();
        user.NormalisedEmail = normalisedEmail;
        user.Role = role;
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        await scope.SaveChangesAsync();

        return user.ToClientAccountDto();
    }

    public async Task<ApiResult<IReadOnlyList<MachinePatDto>>> ListClientAccessTokensAsync(int clientAccountId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var user = userRepository.Get(clientAccountId);
        if (user is null || user.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        var tokens = await personalAccessTokenRepository.GetByUserIdAsync(clientAccountId);
        return tokens.Select(MachinePatRules.ToMachinePatDto).ToArray();
    }

    public async Task<ApiResult<CreatedMachinePatDto>> CreateClientAccessTokenAsync(int clientAccountId, CreateClientAccessTokenRequest request)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(clientAccountId);
        if (user is null || user.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var tokenRequest = new CreateMachinePatRequest(
            request.Name,
            request.ExpiresInDays,
            request.Scopes);

        var patResult = BuildPatEntity(user, tokenRequest, now, MachinePatRules.DefaultClientScopes);
        if (!patResult.Success || patResult.Data is null)
        {
            return ApiResults.BadRequest<CreatedMachinePatDto>(patResult.Message ?? "Token creation failed.", patResult.ValidationErrors);
        }

        personalAccessTokenRepository.Add(patResult.Data.Token);
        await scope.SaveChangesAsync();

        return ApiResults.Created(new CreatedMachinePatDto(
            MachinePatRules.ToMachinePatDto(patResult.Data.Token),
            patResult.Data.PlainTextToken));
    }

    public async Task<ApiResult> RevokeClientAccessTokenAsync(int clientAccountId, int tokenId)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(clientAccountId);
        if (user is null || user.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        var token = await personalAccessTokenRepository.GetByIdAsync(tokenId);
        if (token is null || token.UserId != clientAccountId)
        {
            return ApiErrors.NotFound("Personal access token was not found.");
        }

        if (token.RevokedAtUtc is null)
        {
            token.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            await scope.SaveChangesAsync();
        }

        return ApiResults.Ok();
    }

    public async Task<ApiResult> DeleteClientAccountAsync(int clientAccountId)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(clientAccountId);
        if (user is null || user.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        userRepository.Remove(user);
        await scope.SaveChangesAsync();

        return ApiResults.Ok();
    }

    private static ApiResult<CreatedPat> BuildPatEntity(
        EntityUser user,
        CreateMachinePatRequest request,
        DateTime now,
        IReadOnlyList<string> defaultScopes)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ApiErrors.BadRequest("Token name is required.");
        }

        if (name.Length > 120)
        {
            return ApiErrors.BadRequest("Token name must be 120 characters or fewer.");
        }

        if (request.ExpiresInDays is <= 0 or > 3650)
        {
            return ApiErrors.BadRequest("expiresInDays must be between 1 and 3650 when specified.");
        }

        var requestedScopes = (request.Scopes ?? defaultScopes)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (requestedScopes.Length == 0)
        {
            return ApiErrors.BadRequest("At least one scope is required.");
        }

        if (requestedScopes.Except(MachinePatRules.SupportedClientScopes, StringComparer.OrdinalIgnoreCase).Any())
        {
            return ApiErrors.BadRequest("Unsupported scope provided.");
        }

        var scopes = MachinePatRules.NormaliseScopes(
            request.Scopes,
            defaultScopes,
            MachinePatRules.SupportedClientScopes);
        if (scopes.Count == 0)
        {
            return ApiErrors.BadRequest("At least one scope is required.");
        }

        var plainTextToken = CreatePersonalAccessToken();
        var entity = new EntityPersonalAccessToken
        {
            User = user,
            Name = name,
            TokenHash = HashToken(plainTextToken),
            TokenPrefix = plainTextToken[..12],
            ScopesCsv = string.Join(',', scopes),
            CreatedAtUtc = now,
            ExpiresAtUtc = request.ExpiresInDays is null ? null : now.AddDays(request.ExpiresInDays.Value)
        };

        return ApiResults.Ok(new CreatedPat(entity, plainTextToken));
    }

    private static string CreatePersonalAccessToken() =>
        $"bo_pat_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string CreateRandomPassword() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static IReadOnlyList<ValidationError> ValidateUserNameAndEmail(string userName, string email)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(new ValidationError("userName", "Username is required."));
        }
        else if (userName.Trim().Length is < 1 or > 64)
        {
            errors.Add(new ValidationError("userName", "Username must be between 1 and 64 characters."));
        }

        errors.AddRange(EmailAddressRules.Validate(email, "email"));

        return errors;
    }

    private static bool TryParseRole(string roleValue, out UserRole role)
    {
        if (string.Equals(roleValue, BoardOilRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.Admin;
            return true;
        }

        if (string.Equals(roleValue, BoardOilRoles.Standard, StringComparison.OrdinalIgnoreCase))
        {
            role = UserRole.Standard;
            return true;
        }

        role = default;
        return false;
    }
}
