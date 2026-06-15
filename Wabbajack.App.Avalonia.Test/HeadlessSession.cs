using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;

namespace WabbajackAvalonia.Test;

// One headless Avalonia session per assembly; tests dispatch their bodies onto its UI thread.
// HeadlessUnitTestSession exposes Dispatch<TResult>(Func<Task<TResult>>, CancellationToken) — there is
// no non-generic Func<Task> overload — so the test body returns a value (we use bool) to bind to it.
public static class HeadlessSession
{
    private static readonly Lazy<HeadlessUnitTestSession> Session =
        new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    public static Task<T> Dispatch<T>(Func<Task<T>> body) =>
        Session.Value.Dispatch(body, CancellationToken.None);
}
