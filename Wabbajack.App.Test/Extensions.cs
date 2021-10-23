using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using Wabbajack.App.Models;

namespace Wabbajack.App.Test;

public static class Extensions
{
    public static async Task WaitUntil<T>(this T src, Predicate<T> check, Action? doFunc = null)
    {
        Dispatcher.UIThread.RunJobs();

        while (!check(src))
        {
            doFunc?.Invoke();
            await Task.Delay(100);
        }
    }

    public static async Task WaitForLock(this LoadingLock l)
    {
        Dispatcher.UIThread.RunJobs();
        while (!l.IsLoading)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(100);
        }
    }
    public static async Task WaitForUnlock(this LoadingLock l)
    {
        Dispatcher.UIThread.RunJobs();
        while (l.IsLoading)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(100);
        }
    }
}