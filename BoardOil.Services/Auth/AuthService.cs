using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BoardOil.Abstractions.Auth;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Persistence.Abstractions.Board;
using BoardOil.Persistence.Abstractions.Auth;
using BoardOil.Persistence.Abstractions.Entities;
using BoardOil.Contracts.Auth;
using BoardOil.Contracts.Contracts;

namespace BoardOil.Services.Auth;

public sealed class AuthService(
    IAuthUserRepository authUserRepository,
    IBoardRepository boardRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPersonalAccessTokenRepository personalAccessTokenRepository,
    IPasswordHashService passwordHashService,
    IAccessTokenIssuer accessTokenIssuer,
    AuthSessionOptions sessionOptions,
    TimeProvider timeProvider,
    IDbContextScopeFactory scopeFactory) : IAuthService
{
    public async Task<ApiResult<AuthSessionTokens>> RegisterInitialAdminAsync(RegisterInitialAdminRequest request)
    {
        using var scope = scopeFactory.Create();

        var validation = ValidateCredentials(request.UserName, request.Password);
        if (validation.Count > 0)
        {
            return ApiErrors.BadRequest("Validation failed.", validation);
        }

        if (await authUserRepository.AnyAsync())
        {
            return new ApiError(409, "Initial admin already exists.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accessTokenExpiresAtUtc = now.AddMinutes(sessionOptions.AccessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(sessionOptions.RefreshTokenDays);
        var refreshToken = CreateRefreshToken();
        var existingBoards = await boardRepository.GetBoardsOrderedAsync();
        var user = new EntityUser
        {
            UserName = request.UserName.Trim(),
            PasswordHash = passwordHashService.HashPassword(request.Password),
            Role = UserRole.Admin,
            IdentityType = UserIdentityType.User,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            RefreshTokens =
            [
                new EntityRefreshToken
                {
                    TokenHash = HashRefreshToken(refreshToken),
                    CreatedAtUtc = now,
                    ExpiresAtUtc = refreshTokenExpiresAtUtc
                }
            ],
            BoardMemberships = existingBoards
                .Select(board => new EntityBoardMember
                {
                    BoardId = board.Id,
                    Role = BoardMemberRole.Owner,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                })
                .ToList()
        };

        authUserRepository.Add(user);
        await scope.SaveChangesAsync();

        var accessToken = accessTokenIssuer.CreateAccessToken(user.Id, user.UserName, user.Role.ToString(), now, accessTokenExpiresAtUtc);
        return new AuthSessionTokens(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            CreateCsrfToken(),
            user.ToAuthUserDto());
    }

    public async Task<ApiResult<AuthSessionTokens>> LoginAsync(LoginRequest request)
    {
        using var scope = scopeFactory.Create();

        var normalisedUserName = request.UserName?.Trim();
        if (string.IsNullOrWhiteSpace(normalisedUserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiErrors.Unauthorized("Invalid username or password.");
        }

        var user = await authUserRepository.GetByUserNameAsync(normalisedUserName);
        if (user is null || !user.IsActive || user.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Unauthorized("Invalid username or password.");
        }

        if (!passwordHashService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return ApiErrors.Unauthorized("Invalid username or password.");
        }

        var session = CreateSession(user);
        await scope.SaveChangesAsync();
        return session;
    }

    public async Task<ApiResult<CreatedMachinePatDto>> CreateMachinePatAsync(int userId, CreateMachinePatRequest request)
    {
        using var scope = scopeFactory.Create();

        var user = authUserRepository.Get(userId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (user.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts cannot manage access tokens.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
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

        var requestedScopes = request.Scopes?
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (requestedScopes is { Length: > 0 }
            && requestedScopes.Except(MachinePatRules.SupportedUserScopes, StringComparer.OrdinalIgnoreCase).Any())
        {
            return ApiErrors.BadRequest("Unsupported scope provided.");
        }

        var scopes = MachinePatRules.NormaliseScopes(
            request.Scopes,
            MachinePatRules.DefaultUserScopes,
            MachinePatRules.SupportedUserScopes);
        if (scopes.Count == 0)
        {
            return ApiErrors.BadRequest("At least one scope is required.");
        }

        var boardAccessMode = MachinePatRules.NormaliseBoardAccessMode(request.BoardAccessMode);
        if (!MachinePatRules.SupportedBoardAccessModes.Contains(boardAccessMode, StringComparer.Ordinal))
        {
            return ApiErrors.BadRequest("Unsupported boardAccessMode provided.");
        }

        var allowedBoardIds = MachinePatRules.NormaliseAllowedBoardIds(request.AllowedBoardIds);
        if (boardAccessMode == MachinePatBoardAccessModes.All && allowedBoardIds.Count > 0)
        {
            return ApiErrors.BadRequest("allowedBoardIds must be empty when boardAccessMode is 'all'.");
        }

        if (boardAccessMode == MachinePatBoardAccessModes.Selected && allowedBoardIds.Count == 0)
        {
            return ApiErrors.BadRequest("allowedBoardIds is required when boardAccessMode is 'selected'.");
        }

        var plainTextToken = CreatePersonalAccessToken();
        var entity = new EntityPersonalAccessToken
        {
            UserId = userId,
            Name = name,
            TokenHash = HashToken(plainTextToken),
            TokenPrefix = plainTextToken[..12],
            ScopesCsv = string.Join(',', scopes),
            BoardAccessMode = boardAccessMode,
            AllowedBoardIdsCsv = string.Join(',', allowedBoardIds),
            CreatedAtUtc = now,
            ExpiresAtUtc = request.ExpiresInDays is null ? null : now.AddDays(request.ExpiresInDays.Value)
        };

        personalAccessTokenRepository.Add(entity);
        await scope.SaveChangesAsync();

        return new CreatedMachinePatDto(
            MachinePatRules.ToMachinePatDto(entity),
            plainTextToken);
    }

    public async Task<ApiResult<IReadOnlyList<MachinePatDto>>> ListMachinePatsAsync(int userId)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var user = authUserRepository.Get(userId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (user.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts cannot manage access tokens.");
        }

        var tokens = await personalAccessTokenRepository.GetByUserIdAsync(userId);
        return tokens.Select(MachinePatRules.ToMachinePatDto).ToArray();
    }

    public async Task<ApiResult> RevokeMachinePatAsync(int userId, int tokenId)
    {
        using var scope = scopeFactory.Create();

        var user = authUserRepository.Get(userId);
        if (user is null || !user.IsActive)
        {
            return ApiErrors.Unauthorized("User is not active.");
        }

        if (user.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Forbidden("Client accounts cannot manage access tokens.");
        }

        var token = await personalAccessTokenRepository.GetByIdAsync(tokenId);
        if (token is null || token.UserId != userId)
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

    public async Task<ApiResult<AuthSessionTokens>> RefreshAsync(string? refreshToken)
    {
        using var scope = scopeFactory.Create();

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ApiErrors.Unauthorized("Refresh token is missing.");
        }

        var hash = HashRefreshToken(refreshToken);
        var existingToken = await refreshTokenRepository.GetWithUserByHashAsync(hash);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (existingToken is null
            || existingToken.RevokedAtUtc is not null
            || existingToken.ExpiresAtUtc <= now
            || !existingToken.User.IsActive
            || existingToken.User.IdentityType == UserIdentityType.Client)
        {
            return ApiErrors.Unauthorized("Refresh token is invalid or expired.");
        }

        var newSession = CreateSession(existingToken.User);
        existingToken.RevokedAtUtc = now;
        existingToken.ReplacedByTokenHash = HashRefreshToken(newSession.RefreshToken);
        await scope.SaveChangesAsync();

        return newSession;
    }

    public async Task<ApiResult> LogoutAsync(string? refreshToken)
    {
        using var scope = scopeFactory.Create();

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var hash = HashRefreshToken(refreshToken);
            var existingToken = await refreshTokenRepository.GetByHashAsync(hash);
            if (existingToken is not null && existingToken.RevokedAtUtc is null)
            {
                existingToken.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
                await scope.SaveChangesAsync();
            }
        }

        return ApiResults.Ok();
    }

    public Task<ApiResult<AuthUserDto>> GetMeAsync(ClaimsPrincipal claimsPrincipal)
    {
        using var scope = scopeFactory.CreateReadOnly();

        var userIdClaim = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Task.FromResult<ApiResult<AuthUserDto>>(ApiErrors.Unauthorized("Invalid identity context."));
        }

        var user = authUserRepository.Get(userId);
        if (user is null || !user.IsActive)
        {
            return Task.FromResult<ApiResult<AuthUserDto>>(ApiErrors.Unauthorized("User is not active."));
        }

        return Task.FromResult<ApiResult<AuthUserDto>>(user.ToAuthUserDto());
    }

    public async Task<ApiResult<BootstrapStatusDto>> GetBootstrapStatusAsync()
    {
        using var scope = scopeFactory.CreateReadOnly();

        var hasUsers = await authUserRepository.AnyAsync();
        return new BootstrapStatusDto(!hasUsers);
    }

    public string CreateCsrfToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private AuthSessionTokens CreateSession(EntityUser user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var accessTokenExpiresAtUtc = now.AddMinutes(sessionOptions.AccessTokenMinutes);
        var refreshTokenExpiresAtUtc = now.AddDays(sessionOptions.RefreshTokenDays);

        var accessToken = accessTokenIssuer.CreateAccessToken(user.Id, user.UserName, user.Role.ToString(), now, accessTokenExpiresAtUtc);
        var refreshToken = CreateRefreshToken();
        refreshTokenRepository.Add(new EntityRefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshTokenExpiresAtUtc
        });

        return new AuthSessionTokens(
            accessToken,
            accessTokenExpiresAtUtc,
            refreshToken,
            refreshTokenExpiresAtUtc,
            CreateCsrfToken(),
            user.ToAuthUserDto());
    }

    private static string CreateRefreshToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static string CreatePersonalAccessToken() =>
        $"bo_pat_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32))}";

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static IReadOnlyList<string> ParseScopes(string scopesCsv)
    {
        return MachinePatRules.ParseScopes(scopesCsv);
    }

    private static IReadOnlyList<string> NormaliseScopes(IEnumerable<string>? scopes) =>
        MachinePatRules.NormaliseScopes(
            scopes,
            MachinePatRules.DefaultUserScopes,
            MachinePatRules.SupportedUserScopes);

    private static string NormaliseBoardAccessMode(string? boardAccessMode) =>
        MachinePatRules.NormaliseBoardAccessMode(boardAccessMode);

    private static IReadOnlyList<int> ParseAllowedBoardIds(string allowedBoardIdsCsv) =>
        MachinePatRules.ParseAllowedBoardIds(allowedBoardIdsCsv);

    private static IReadOnlyList<int> NormaliseAllowedBoardIds(IEnumerable<int>? allowedBoardIds) =>
        MachinePatRules.NormaliseAllowedBoardIds(allowedBoardIds);

    private static MachinePatDto ToMachinePatDto(EntityPersonalAccessToken token) =>
        MachinePatRules.ToMachinePatDto(token);

    private static IReadOnlyList<ValidationError> ValidateCredentials(string userName, string password)
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

        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add(new ValidationError("password", "Password is required."));
        }
        else if (password.Length < 10)
        {
            errors.Add(new ValidationError("password", "Password must be at least 10 characters."));
        }

        return errors;
    }
}
