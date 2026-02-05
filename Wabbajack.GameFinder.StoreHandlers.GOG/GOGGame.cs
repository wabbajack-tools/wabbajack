using System;
using Wabbajack.GameFinder.Common;
using JetBrains.Annotations;
using Wabbajack.GameFinder.Paths;

namespace Wabbajack.GameFinder.StoreHandlers.GOG;

/// <summary>
/// Represents a game installed with GOG Galaxy.
/// </summary>
[PublicAPI]
public record GOGGame(GOGGameId Id, string Name, AbsolutePath Path, ulong BuildId, Nullable<GOGGameId> ParentGameId = null) : IGame;
