using LinqToDB;

namespace Outbox.WebApi.Linq2db;

public static class PostgreSqlExtensions
{
    [Sql.Expression(ProviderName.PostgreSQL, "pg_snapshot_xmin(pg_current_snapshot())", ServerSideOnly = true)]
    public static ulong MinCurrentTransactionId => throw new NotImplementedException();
}
