using LinqToDB;

namespace Outbox.WebApi.Linq2db;

public static class PostgreSqlExtensions
{
    [Sql.Expression(ProviderName.PostgreSQL, "pg_snapshot_xmin(pg_current_snapshot())", ServerSideOnly = true)]
    public static ulong MinCurrentTransactionId => throw new NotImplementedException();

    [Sql.Expression(ProviderName.PostgreSQL, "({0}, {1}) > ({2}::text::xid8, {3})", ServerSideOnly = true)]
    public static bool GreaterThan<T1, T2>(T1 v1, T2 v2, T1 v3, T2 v4) => throw new NotImplementedException();
}