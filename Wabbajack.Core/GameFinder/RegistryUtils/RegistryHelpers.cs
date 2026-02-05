using System;
using JetBrains.Annotations;

namespace Wabbajack.GameFinder.RegistryUtils;

[PublicAPI]
public static class RegistryHelpers
{
    public static string RegistryHiveToString(this RegistryHive hive)
    {
        return hive switch
        {
            RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
            RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
            RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
            RegistryHive.Users => "HKEY_USERS",
            RegistryHive.PerformanceData => "HKEY_PERFORMANCE_DATA",
            RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
            _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, message: null),
        };
    }

    public static RegistryHive RegistryHiveFromString(string hive)
    {
        return hive switch
        {
            "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            "HKEY_USERS" => RegistryHive.Users,
            "HKEY_PERFORMANCE_DATA" => RegistryHive.PerformanceData,
            "HKEY_CURRENT_CONFIG" => RegistryHive.CurrentConfig,
            _ => throw new ArgumentOutOfRangeException(nameof(hive), hive, message: null),
        };
    }
}
