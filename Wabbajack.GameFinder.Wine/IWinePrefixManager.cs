using System.Collections.Generic;
using Wabbajack.GameFinder.Common;
using JetBrains.Annotations;
using OneOf;

namespace Wabbajack.GameFinder.Wine;

/// <summary>
/// Implementation for wine prefix managers.
/// </summary>
/// <typeparam name="TPrefix"></typeparam>
[PublicAPI]
public interface IWinePrefixManager<TPrefix> where TPrefix : AWinePrefix
{
    /// <summary>
    /// Finds all prefixes associated with this manager.
    /// </summary>
    /// <returns></returns>
    IEnumerable<OneOf<TPrefix, ErrorMessage>> FindPrefixes();
}
