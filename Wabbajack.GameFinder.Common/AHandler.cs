using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using OneOf;

namespace Wabbajack.GameFinder.Common;

/// <summary>
/// Base class for store handlers.
/// </summary>
/// <seealso cref="AHandler{TGame,TId}"/>
[PublicAPI]
public abstract class AHandler
{
    /// <summary>
    /// Finds all <see cref="IGame"/> instances installed with this store.
    /// </summary>
    /// <returns></returns>
    /// <seealso cref="AHandler{TGame,TId}.FindAllGames"/>
    [MustUseReturnValue]
    [System.Diagnostics.Contracts.Pure]
    public abstract IEnumerable<OneOf<IGame, ErrorMessage>> FindAllInterfaceGames();
}

/// <summary>
/// Generic base class for store handlers.
/// </summary>
/// <typeparam name="TGame"></typeparam>
/// <typeparam name="TId"></typeparam>
/// <seealso cref="AHandler"/>
[PublicAPI]
public abstract class AHandler<TGame, TId> : AHandler
    where TGame : class, IGame
    where TId : notnull
{
    /// <summary>
    /// Method that accepts a <typeparamref name="TGame"/> and returns the
    /// <typeparamref name="TId"/> of it. This is useful for constructing
    /// key-based data types like <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    public abstract Func<TGame, TId> IdSelector { get; }

    /// <summary>
    /// Custom equality comparer for <typeparamref name="TId"/>. This is useful
    /// for constructing key-based data types like <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    public abstract IEqualityComparer<TId>? IdEqualityComparer { get; }

    /// <inheritdoc/>
    [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
    public override IEnumerable<OneOf<IGame, ErrorMessage>> FindAllInterfaceGames()
    {
        foreach (var res in FindAllGames())
        {
            yield return res.MapT0(x => (IGame)x);
        }
    }

    /// <summary>
    /// Finds all games installed with this store.
    /// </summary>
    /// <returns></returns>
    [MustUseReturnValue]
    [System.Diagnostics.Contracts.Pure]
    public abstract IEnumerable<OneOf<TGame, ErrorMessage>> FindAllGames();

    /// <summary>
    /// Calls <see cref="FindAllGames"/> and converts the result into a dictionary where
    /// the key is the id of the game.
    /// </summary>
    /// <param name="errors"></param>
    /// <returns></returns>
    [MustUseReturnValue]
    [System.Diagnostics.Contracts.Pure]
    public IReadOnlyDictionary<TId, TGame> FindAllGamesById(out ErrorMessage[] errors)
    {
        var (games, allErrors) = FindAllGames().SplitResults();
        errors = allErrors;

        return games.CustomToDictionary(IdSelector, game => game, IdEqualityComparer ?? EqualityComparer<TId>.Default);
    }

    /// <summary>
    /// Wrapper around <see cref="FindAllGamesById"/> if you just need to find one game.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="errors"></param>
    /// <returns></returns>
    [MustUseReturnValue]
    [System.Diagnostics.Contracts.Pure]
    public TGame? FindOneGameById(TId id, out ErrorMessage[] errors)
    {
        var allGames = FindAllGamesById(out errors);
        return allGames.TryGetValue(id, out var game) ? game : null;
    }
}
