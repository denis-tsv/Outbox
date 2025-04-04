using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Outbox.Extensions;

namespace Outbox.WebApi.EFCore;

public class ForUpdateInterceptor : DbCommandInterceptor
{
    private static string AddHint(string commandText)
    {
        if (commandText.Contains(QueryHintsExtensions.HintForUpdateSkipLocked, StringComparison.InvariantCulture))
            return commandText + Environment.NewLine + "FOR UPDATE SKIP LOCKED";
        if (commandText.Contains(QueryHintsExtensions.HintForUpdate, StringComparison.InvariantCulture))
            return commandText + Environment.NewLine + "FOR UPDATE";
        return commandText;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        command.CommandText = AddHint(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        command.CommandText = AddHint(command.CommandText);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        command.CommandText = AddHint(command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = new())
    {
        command.CommandText = AddHint(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = new())
    {
        command.CommandText = AddHint(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = new())
    {
        command.CommandText = AddHint(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }
}