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

        var validation = ValidateUserName(request.UserName);
        if (validation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validation);
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return ApiErrors.BadRequest("Role must be 'Admin' or 'Standard'.");
        }

        var userName = request.UserName.Trim();
        var exists = await userRepository.UserNameExistsAsync(userName);
        if (exists)
        {
            return ApiErrors.BadRequest("Username already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = new EntityUser
        {
            UserName = userName,
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
            request.Scopes,
            request.BoardAccessMode,
            request.AllowedBoardIds);

        var patResult = BuildPatEntity(user, tokenRequest, now, MachinePatRules.DefaultClientScopes, forceAllBoards: true);
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

    public async Task<ApiResult<CreatedMachinePatDto>> CreateClientAccessTokenAsync(int clientAccountId, CreateMachinePatRequest request)
    {
        using var scope = scopeFactory.Create();

        var user = userRepository.Get(clientAccountId);
        if (user is null || user.IdentityType != UserIdentityType.Client)
        {
            return ApiErrors.NotFound("Client account not found.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var patResult = BuildPatEntity(user, request, now, MachinePatRules.DefaultClientScopes, forceAllBoards: true);
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

    private static ApiResult<CreatedPat> BuildPatEntity(
        EntityUser user,
        CreateMachinePatRequest request,
        DateTime now,
        IReadOnlyList<string> defaultScopes,
        bool forceAllBoards)
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

        if (requestedScopes.Except(MachinePatRules.SupportedScopes, StringComparer.OrdinalIgnoreCase).Any())
        {
            return ApiErrors.BadRequest("Unsupported scope provided.");
        }

        var scopes = MachinePatRules.NormaliseScopes(request.Scopes, defaultScopes);
        if (scopes.Count == 0)
        {
            return ApiErrors.BadRequest("At least one scope is required.");
        }

        var boardAccessMode = forceAllBoards
            ? MachinePatBoardAccessModes.All
            : MachinePatRules.NormaliseBoardAccessMode(request.BoardAccessMode);
        if (!MachinePatRules.SupportedBoardAccessModes.Contains(boardAccessMode, StringComparer.Ordinal))
        {
            return ApiErrors.BadRequest("Unsupported boardAccessMode provided.");
        }

        var allowedBoardIds = forceAllBoards
            ? Array.Empty<int>()
            : MachinePatRules.NormaliseAllowedBoardIds(request.AllowedBoardIds);
        if (!forceAllBoards && boardAccessMode == MachinePatBoardAccessModes.All && allowedBoardIds.Count > 0)
        {
            return ApiErrors.BadRequest("allowedBoardIds must be empty when boardAccessMode is 'all'.");
        }

        if (!forceAllBoards && boardAccessMode == MachinePatBoardAccessModes.Selected && allowedBoardIds.Count == 0)
        {
            return ApiErrors.BadRequest("allowedBoardIds is required when boardAccessMode is 'selected'.");
        }

        var plainTextToken = CreatePersonalAccessToken();
        var entity = new EntityPersonalAccessToken
        {
            User = user,
            Name = name,
            TokenHash = HashToken(plainTextToken),
            TokenPrefix = plainTextToken[..12],
            ScopesCsv = string.Join(',', scopes),
            BoardAccessMode = boardAccessMode,
            AllowedBoardIdsCsv = string.Join(',', allowedBoardIds),
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

    private static IReadOnlyList<ValidationError> ValidateUserName(string userName)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add(new ValidationError("userName", "Username is required."));
        }
        else if (userName.Trim().Length is < 3 or > 64)
        {
            errors.Add(new ValidationError("userName", "Username must be between 3 and 64 characters."));
        }

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
