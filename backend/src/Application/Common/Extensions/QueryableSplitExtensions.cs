using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// Cung cấp extension AsSplitQuery cho IQueryable ở tầng Application (tránh Cartesian Explosion khi Include nhiều collection)
/// mà không bắt tầng Application phải phụ thuộc trực tiếp vào gói Microsoft.EntityFrameworkCore.Relational.
/// </summary>
internal static class QueryableSplitExtensions
{
    private static readonly MethodInfo? RelationalAsSplitQueryMethod = ResolveAsSplitQueryMethod();

    public static IQueryable<TEntity> AsSplitQuery<TEntity>(this IQueryable<TEntity> source)
        where TEntity : class
    {
        if (RelationalAsSplitQueryMethod is null)
        {
            return source;
        }

        try
        {
            var genericMethod = RelationalAsSplitQueryMethod.MakeGenericMethod(typeof(TEntity));
            if (genericMethod.Invoke(null, [source]) is IQueryable<TEntity> splitQueryable)
            {
                return splitQueryable;
            }
        }
        catch
        {
            // Fallback an toàn cho In-Memory / Mock IQueryable trong Unit Tests
        }

        return source;
    }

    private static MethodInfo? ResolveAsSplitQueryMethod()
    {
        try
        {
            var relationalExtType = Type.GetType(
                "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions, Microsoft.EntityFrameworkCore.Relational");
            return relationalExtType?.GetMethod(
                "AsSplitQuery",
                BindingFlags.Public | BindingFlags.Static);
        }
        catch
        {
            return null;
        }
    }
}
