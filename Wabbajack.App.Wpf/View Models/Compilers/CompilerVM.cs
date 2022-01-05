using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Wabbajack.Extensions;
using Wabbajack.Interventions;
using Wabbajack.Messages;
using Wabbajack.RateLimiter;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Binding;
using ReactiveUI.Fody.Helpers;
using Wabbajack.DTOs.Interventions;

namespace Wabbajack
{
    public class CompilerVM : BackNavigatingVM
    {
        public CompilerVM(ILogger<CompilerVM> logger) : base(logger)
        {
        }
    }
}
