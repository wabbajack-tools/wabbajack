using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JetBrains.Annotations;
using static Wabbajack.GameFinder.Paths.Delegates;

namespace Wabbajack.GameFinder.Paths;

/// <summary>
/// Provides information about the current operating system.
/// </summary>
[PublicAPI]
// ReSharper disable once InconsistentNaming
public interface IOSInformation
{
    /// <summary>
    /// The current <see cref="OSPlatform"/>.
    /// </summary>
    OSPlatform Platform { get; }

    /// <summary>
    /// Whether the current <see cref="Platform"/> is <see cref="OSPlatform.Windows"/>.
    /// </summary>
    [SupportedOSPlatformGuard("windows")]
    bool IsWindows => Platform == OSPlatform.Windows;

    /// <summary>
    /// Whether the current <see cref="Platform"/> is <see cref="OSPlatform.Linux"/>.
    /// </summary>
    [SupportedOSPlatformGuard("linux")]
    bool IsLinux => Platform == OSPlatform.Linux;

    /// <summary>
    /// Whether the current <see cref="Platform"/> is <see cref="OSPlatform.OSX"/>.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = $"It's also named {nameof(OSPlatform.OSX)} in {nameof(OSPlatform)}")]
    [SupportedOSPlatformGuard("osx")]
    bool IsOSX => Platform == OSPlatform.OSX;

    /// <summary>
    /// Matches and returns a value based on the current platform.
    /// </summary>
    /// <param name="onWindows"></param>
    /// <param name="onLinux"></param>
    /// <param name="onOSX"></param>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
    /// <seealso cref="MatchPlatform{TOut}"/>
    /// <seealso cref="SwitchPlatform"/>
    /// <seealso cref="SwitchPlatform{TState}"/>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = $"It's also named {nameof(OSPlatform.OSX)} in {nameof(OSPlatform)}")]
    TOut MatchPlatform<TOut>(
        Func<TOut>? onWindows = null,
        Func<TOut>? onLinux = null,
        Func<TOut>? onOSX = null)
    {
        Func<TOut>? func = null;

        if (IsWindows) func = onWindows;
        else if (IsLinux) func = onLinux;
        else if (IsOSX) func = onOSX;
        else ThrowUnsupported();

        if (func is null) ThrowUnsupported();
        return func();
    }

    /// <summary>
    /// Matches and returns a value based on the current platform and allows
    /// <paramref name="state"/> to be passed to the each handler, preventing lambda allocations.
    /// </summary>
    /// <param name="onWindows"></param>
    /// <param name="onLinux"></param>
    /// <param name="onOSX"></param>
    /// <param name="state"></param>
    /// <typeparam name="TState"></typeparam>
    /// <typeparam name="TOut"></typeparam>
    /// <returns></returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
    /// <seealso cref="MatchPlatform{TOut}"/>
    /// <seealso cref="SwitchPlatform"/>
    /// <seealso cref="SwitchPlatform{TState}"/>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = $"It's also named {nameof(OSPlatform.OSX)} in {nameof(OSPlatform)}")]
    TOut MatchPlatform<TState, TOut>(
        ref TState state,
        FuncRef<TState, TOut>? onWindows = null,
        FuncRef<TState, TOut>? onLinux = null,
        FuncRef<TState, TOut>? onOSX = null)
    {
        FuncRef<TState, TOut>? func = null;

        if (IsWindows) func = onWindows;
        else if (IsLinux) func = onLinux;
        else if (IsOSX) func = onOSX;
        else ThrowUnsupported();

        if (func is null) ThrowUnsupported();
        return func(ref state);
    }

    /// <summary>
    /// Switches on the current platform.
    /// </summary>
    /// <param name="onWindows"></param>
    /// <param name="onLinux"></param>
    /// <param name="onOSX"></param>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
    /// <seealso cref="SwitchPlatform{TState}"/>
    /// <seealso cref="MatchPlatform{TOut}"/>
    /// <seealso cref="MatchPlatform{TState, TOut}"/>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = $"It's also named {nameof(OSPlatform.OSX)} in {nameof(OSPlatform)}")]
    void SwitchPlatform(
        Action? onWindows = null,
        Action? onLinux = null,
        Action? onOSX = null)
    {
        Action? action = null;

        if (IsWindows) action = onWindows;
        else if (IsLinux) action = onLinux;
        else if (IsOSX) action = onOSX;
        else ThrowUnsupported();

        if (action is null) ThrowUnsupported();
        action();
    }

    /// <summary>
    /// Switches on the current platform and allows <paramref name="state"/> to be
    /// passed to each handler, preventing lambda allocations.
    /// </summary>
    /// <param name="onWindows"></param>
    /// <param name="onLinux"></param>
    /// <param name="onOSX"></param>
    /// <param name="state"></param>
    /// <typeparam name="TState"></typeparam>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
    /// <seealso cref="SwitchPlatform"/>
    /// <seealso cref="MatchPlatform{TOut}"/>
    /// <seealso cref="MatchPlatform{TState, TOut}"/>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = $"It's also named {nameof(OSPlatform.OSX)} in {nameof(OSPlatform)}")]
    void SwitchPlatform<TState>(
        ref TState state,
        ActionRef<TState>? onWindows = null,
        ActionRef<TState>? onLinux = null,
        ActionRef<TState>? onOSX = null)
    {
        ActionRef<TState>? action = null;

        if (IsWindows) action = onWindows;
        else if (IsLinux) action = onLinux;
        else if (IsOSX) action = onOSX;
        else ThrowUnsupported();

        if (action is null) ThrowUnsupported();
        action(ref state);
    }

    /// <summary>
    /// Returns <c>true</c> if the current platform <see cref="Platform"/> is supported.
    /// </summary>
    /// <returns></returns>
    bool IsPlatformSupported()
    {
        return IsWindows || IsLinux || IsOSX;
    }

    /// <summary>
    /// Returns <c>true</c> if the current platform is Unix-based.
    /// </summary>
    /// <returns></returns>
    [UnsupportedOSPlatformGuard("windows")]
    bool IsUnix() => IsLinux || IsOSX;

    /// <summary>
    /// Throws <see cref="PlatformNotSupportedException"/> for the current platform.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException"/>
    [DoesNotReturn]
    void ThrowUnsupported() => throw new PlatformNotSupportedException($"The operation or feature isn't unsupported on the current platform `{Platform}`");

    /// <summary>
    /// Throws <see cref="PlatformNotSupportedException"/> for the current platform.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException"/>
    [DoesNotReturn]
    T ThrowUnsupported<T>()
    {
        ThrowUnsupported();
        return default;
    }

    /// <summary>
    /// Guard statement for platform support.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when the current platform is not supported.</exception>
    [Obsolete(message: $"Use {nameof(ThrowUnsupported)} instead")]
    void PlatformSupportedGuard()
    {
        if (!IsPlatformSupported())
            throw CreatePlatformNotSupportedException();
    }

    /// <summary>
    /// Creates a new <see cref="PlatformNotSupportedException"/>.
    /// </summary>
    /// <returns></returns>
    [Obsolete(message: $"Use {nameof(ThrowUnsupported)} instead")]
    PlatformNotSupportedException CreatePlatformNotSupportedException()
    {
        return new PlatformNotSupportedException($"The current platform is not supported: {Platform}");
    }
}
