using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace Wabbajack.Web.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable)
        {
            return enumerable.Where(x => x != null).Select(x => x!);
        }
    }
}
