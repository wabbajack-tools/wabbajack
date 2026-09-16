using System;

namespace Wabbajack.Networking.Bethesda.Steam;

/// <summary>
///     The two things version probing needs from a loaded <c>steam_api64.dll</c>: look a symbol up, and call
///     one of the no-argument interface accessors.
///     <para>
///         It exists so that <see cref="SteamApiInterfaceProbe" /> - the part that decides which SDK
///         generation is in front of it, and the part most likely to be wrong - can be tested against a table
///         of exports rather than against a game install and a running Steam client.
///     </para>
/// </summary>
public interface ISteamApiExports
{
    /// <summary>The address of an exported symbol, or <see cref="IntPtr.Zero" /> when it is not exported.</summary>
    IntPtr GetExport(string name);

    /// <summary>
    ///     Calls an accessor obtained from <see cref="GetExport" /> and returns the interface pointer it gave
    ///     back, which is <see cref="IntPtr.Zero" /> when Steam declined to hand that interface over.
    /// </summary>
    IntPtr CallInterfaceAccessor(IntPtr export);
}
