using System;
using System.Collections.Concurrent;
using System.Security.Principal;

#nullable disable

namespace Wabbajack.Common.IO
{
    /// <summary>
    /// A collection of properties to retrieve specific file system paths for the current user.
    /// </summary>
    public static class KnownFolders
    {
        // ---- MEMBERS ------------------------------------------------------------------------------------------------

        private static ConcurrentDictionary<KnownFolderType, KnownFolder> _knownFolderInstances;

        // ---- PROPERTIES ---------------------------------------------------------------------------------------------

        /// <summary>
        /// The per-user Downloads folder.
        /// Defaults to &quot;%USERPROFILE%\Downloads&quot;.
        /// </summary>
        public static KnownFolder Downloads => GetInstance(KnownFolderType.Downloads);

        /// <summary>
        /// The per-user Local folder.
        /// Defaults to &quot;%LOCALAPPDATA%&quot; (&quot;%USERPROFILE%\AppData\Local&quot;)&quot;.
        /// </summary>
        public static KnownFolder LocalAppData => GetInstance(KnownFolderType.LocalAppData);

        /// <summary>
        /// The fixed System32 folder (32-bit forced).
        /// This is the same as the <see cref="System"/> known folder in 32-bit applications.
        /// Points to &quot;%WINDIR%\syswow64&quot; in 64-bit applications or in 32-bit applications on a 64-bit
        /// operating system and to &quot;%WINDIR%\system32&quot; on 32-bit operating systems.
        /// </summary>
        public static KnownFolder SystemX86 => GetInstance(KnownFolderType.SystemX86);

        // ---- METHODS (PRIVATE) --------------------------------------------------------------------------------------
        
        private static KnownFolder GetInstance(KnownFolderType type)
        {
            // Check if the caching directory exists yet.
            _knownFolderInstances ??= new ConcurrentDictionary<KnownFolderType, KnownFolder>();

            // Get a KnownFolder instance out of the cache dictionary or create it when not cached yet.
            if (_knownFolderInstances.TryGetValue(type, out KnownFolder knownFolder))
                return knownFolder;

#if WINDOWS
            knownFolder = new KnownFolder(type, System.Security.Principal.WindowsIdentity.GetCurrent());
            return _knownFolderInstances.TryAdd(type, knownFolder) ? knownFolder : _knownFolderInstances[type];      
#endif
#if LINUX
            knownFolder = new KnownFolder();
            var path = type switch
            {
                //~/Downloads
                KnownFolderType.Downloads => LinuxUtils.GetHomeFolder().Combine("Downloads").ToString(),
                //~/.local/share
                KnownFolderType.LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                //only used to find compact.exe which does not exist on linux
                KnownFolderType.SystemX86 => LinuxUtils.GetHomeFolder().ToString(),
                _ => throw new NotImplementedException($"KnownFolder {type} is not supported on Linux at the moment!")
            };
            return _knownFolderInstances.TryAdd(type, knownFolder) ? knownFolder : _knownFolderInstances[type];
#endif
        }
    }
}
