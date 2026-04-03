namespace BoardOil.Contracts.Board;

public sealed record BoardMemberDto(
    int UserId,
    string UserName,
    string Role,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AddBoardMemberRequest(
    int UserId,
    string Role);

public sealed record UpdateBoardMemberRoleRequest(
    string Role);
