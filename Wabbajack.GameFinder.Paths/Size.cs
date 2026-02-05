using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths.Extensions;
using TransparentValueObjects;

// ReSharper disable InconsistentNaming

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// There are many cases where a value in the app should be a positive number attached to the size of some data,
/// instead of leaving this data unmarked, we wrap it in a readonly value struct to make it explicit.
///
/// Several arithmetic operators only make sense when one side of the operation has undefined units. For example,
/// 1MB * 1MB is technically 1MB^2, but we don't want to allow that because it's not a valid size.
/// </summary>
[ValueObject<ulong>]
[PublicAPI]
public readonly partial struct Size :
    IAugmentWith<JsonAugment>,
    IAdditionOperators<Size, Size, Size>,
    ISubtractionOperators<Size, Size, Size>,
    IDivisionOperators<Size, Size, double>,
    IDivisionOperators<Size, double, Size>,
    IDivisionOperators<Size, TimeSpan, Bandwidth>,
    IMultiplyOperators<Size, double, Size>,
    IComparisonOperators<Size, Size, bool>,
    IMultiplicativeIdentity<Size, Size>,
    IAdditiveIdentity<Size, Size>
{
    /// <summary>
    /// A size that represents 'zero'.
    /// </summary>
    public static readonly Size Zero = FromLong(0);

    /// <summary>
    /// A size that represents 'one'.
    /// </summary>
    public static readonly Size One = FromLong(1);

    /// <inheritdoc />
    public static Size MultiplicativeIdentity => One;

    /// <inheritdoc />
    public static Size AdditiveIdentity => Zero;

    /// <summary>
    /// Converts a long to a Size object.
    /// </summary>
    public static Size FromLong(long value) => From((ulong)value);

    /// <summary>
    /// Represents a size of 1 KiB. (1024 bytes)
    /// </summary>
    public static Size KB => FromLong(1024);

    /// <summary>
    /// Represents a size of 1 MiB. (1024^2 bytes)
    /// </summary>
    public static Size MB => FromLong(1024 * 1024);

    /// <summary>
    /// Represents a size of 1 GiB. (1024^3 bytes)
    /// </summary>
    public static Size GB => FromLong(1024 * 1024 * 1024);

    /// <summary>
    /// Represents a size of 1 TiB. (1024^4 bytes)
    /// </summary>
    public static Size TB => FromLong(1024L * 1024 * 1024 * 1024);

    /// <inheritdoc />
    public static Size operator /(Size left, double right) => From((ulong)(left.Value / right));

    /// <inheritdoc />
    public static Size operator *(Size left, double right) => From((ulong)(left.Value * right));

    /// <inheritdoc />
    public override string ToString() => Value.ToFileSizeString();

    /// <inheritdoc />
    public static Bandwidth operator /(Size left, TimeSpan right)
    {
        return Bandwidth.From((ulong)(left.Value / right.TotalSeconds));
    }

    /// <inheritdoc />
    public static Size operator +(Size left, Size right) => From(left.Value + right.Value);

    /// <inheritdoc />
    public static Size operator -(Size left, Size right) => From(left.Value - right.Value);

    /// <inheritdoc />
    public static double operator /(Size left, Size right) => (double)left.Value / right.Value;

}

/// <summary>
/// Extensions related to <see cref="Size"/> class.
/// </summary>
[PublicAPI]
public static class SizeExtensions
{
    /// <summary>
    /// Returns a sum of all of the sizes in the collection.
    /// </summary>
    /// <param name="coll">The collection to pull the sizes from.</param>
    /// <param name="selector">Selects the size from Type <typeparamref name="T"/></param>
    /// <typeparam name="T">Item to extract size from.</typeparam>
    /// <returns>The total sum.</returns>
    public static Size Sum<T>(this IEnumerable<T> coll, Func<T, Size> selector)
    {
        return coll.Aggregate(Size.Zero, (s, itm) => selector(itm) + s);
    }

}
