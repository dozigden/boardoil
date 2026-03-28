using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BoardOil.Ef;

public sealed class BoardOilDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BoardOilDbContext>
{
    public BoardOilDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("BOARDOIL_DESIGNTIME_CONNECTION_STRING")
            ?? "Data Source=boardoil.designtime.db";

        var options = new DbContextOptionsBuilder<BoardOilDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new BoardOilDbContext(options);
    }
}
