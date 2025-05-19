using System.Text.Json;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using LinqToDB.Mapping;
using LinqToDB.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Outbox.WebApi.Linq2db;

public class OutboxLinqToDBForEFToolsImpl : LinqToDBForEFToolsImplDefault
{
    private readonly string _connectionsString;

    public OutboxLinqToDBForEFToolsImpl(string connectionsString) => _connectionsString = connectionsString;

    public override EFConnectionInfo ExtractConnectionInfo(IDbContextOptions? options) =>
        new()
        {
            ConnectionString = _connectionsString,
        };

    public override MappingSchema CreateMappingSchema(IModel model, IMetadataReader? metadataReader, IValueConverterSelector? convertorSelector, DataOptions dataOptions)
    {
        var result = base.CreateMappingSchema(model, metadataReader, convertorSelector, dataOptions);

        //default parameter type for ulong is decimal, it produces an error on mapping to xid8
        result.SetConvertExpression<ulong, DataParameter>(value => new DataParameter(null, value, DataType.UInt64));

        result.SetConverter<string, Dictionary<string, string>>(str => JsonSerializer.Deserialize<Dictionary<string, string>>(str) ?? new Dictionary<string, string>());
        result.SetConverter<Dictionary<string, string>, string>(dict => JsonSerializer.Serialize(dict));
        
        return result;
    }
}