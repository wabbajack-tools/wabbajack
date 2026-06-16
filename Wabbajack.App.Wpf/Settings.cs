using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Wabbajack.Downloaders;
using Wabbajack.DTOs.JsonConverters;
using Wabbajack.Paths;
using Wabbajack.RateLimiter;
using Wabbajack.Services.OSIntegrated;
using Wabbajack.Util;

namespace Wabbajack;

[JsonName("Mo2ModListInstallerSettings")]
public class Mo2ModlistInstallationSettings
{
    public AbsolutePath InstallationLocation { get; set; }
    public AbsolutePath DownloadLocation { get; set; }
    public bool AutomaticallyOverrideExistingInstall { get; set; }
}
