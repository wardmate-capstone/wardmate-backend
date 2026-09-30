using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

internal static class JsonbMapping
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;

    public static PropertyBuilder<T> AsJsonb<T>(this PropertyBuilder<T> property)
    {
        property.HasConversion(new ValueConverter<T, string>(value => Serialize(value), json => Deserialize<T>(json)));
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => Serialize(left) == Serialize(right),
            value => StringComparer.Ordinal.GetHashCode(Serialize(value)),
            value => Deserialize<T>(Serialize(value))));
        return property.HasColumnType("jsonb");
    }
}
