using System.Data;
using System.Globalization;
using BoardOil.Abstractions.DataAccess;
using BoardOil.Data.Abstractions.Card;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BoardOil.Ef.Card;

public sealed class BoardCardIdAllocator(IAmbientDbContextLocator ambientDbContextLocator) : IBoardCardIdAllocator
{
    public async Task<int> AllocateNextAsync(int boardId, CancellationToken cancellationToken = default)
    {
        if (boardId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boardId), "Board ID must be greater than zero.");
        }

        var dbContext = ambientDbContextLocator.Get<BoardOilDbContext>()
            ?? throw new InvalidOperationException(
                "No ambient DbContext. Board card ID allocation requires an explicit database transaction scope.");
        var currentTransaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Board card ID allocation requires an explicit database transaction.");

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException(
                "Board card ID allocation requires the transaction connection to be open.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction.GetDbTransaction();
        command.CommandText =
            """
            UPDATE "BoardCardIdSequences"
            SET "NextCardId" = "NextCardId" + 1
            WHERE "BoardId" = $boardId
            RETURNING "NextCardId" - 1;
            """;

        var boardIdParameter = command.CreateParameter();
        boardIdParameter.ParameterName = "$boardId";
        boardIdParameter.Value = boardId;
        command.Parameters.Add(boardIdParameter);

        var allocatedValue = await command.ExecuteScalarAsync(cancellationToken);
        if (allocatedValue is null || allocatedValue == DBNull.Value)
        {
            throw new InvalidOperationException($"Board card ID sequence was not found for board '{boardId}'.");
        }

        return Convert.ToInt32(allocatedValue, CultureInfo.InvariantCulture);
    }
}
