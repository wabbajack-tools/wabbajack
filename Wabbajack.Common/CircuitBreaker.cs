using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Wabbajack.Common;

public static class CircuitBreaker
{
    public static TimeSpan DEFAULT_DELAY = TimeSpan.FromMilliseconds(100);
    public static int DEFAULT_DELAY_MULTIPLIER = 2;
    public static int DEFAULT_RETRIES = 5;

    public static async ValueTask<TR> WithAutoRetryAsync<TR, TE>(ILogger logger, Func<Task<TR>> f,
        TimeSpan? delay = null, int? multipler = null, int? maxRetries = null) where TE : Exception
    {
        var retries = 0;
        delay ??= DEFAULT_DELAY;
        multipler ??= DEFAULT_DELAY_MULTIPLIER;
        maxRetries ??= DEFAULT_RETRIES;

        TOP:
        try
        {
            return await f();
        }
        catch (TE ex)
        {
            retries += 1;
            if (retries > maxRetries)
                throw;
            logger.LogWarning("(Retry {retries} of {maxRetries}), got exception {message}, waiting {wait}ms",
                retries, maxRetries, ex.Message, delay!.Value.TotalMilliseconds);
            await Task.Delay(delay.Value);
            delay = delay * multipler;
            goto TOP;
        }
    }

    public static async ValueTask WithAutoRetryAsync<TE>(ILogger logger, Func<Task> f, TimeSpan? delay = null,
        int? multipler = null, int? maxRetries = null) where TE : Exception
    {
        var retries = 0;
        delay ??= DEFAULT_DELAY;
        multipler ??= DEFAULT_DELAY_MULTIPLIER;
        maxRetries ??= DEFAULT_RETRIES;

        TOP:
        try
        {
            await f();
        }
        catch (TE ex)
        {
            retries += 1;
            if (retries > maxRetries)
                throw;
            logger.LogWarning("(Retry {retries} of {maxRetries}), got exception {message}, waiting {wait}ms",
                retries, maxRetries, ex.Message, delay!.Value.TotalMilliseconds);
            await Task.Delay(delay.Value);
            delay = delay * multipler;
            goto TOP;
        }
    }


    public static async ValueTask WithAutoRetryAllAsync(ILogger logger, Func<ValueTask> f, TimeSpan? delay = null,
        int? multipler = null, int? maxRetries = null)
    {
        var retries = 0;
        delay ??= DEFAULT_DELAY;
        multipler ??= DEFAULT_DELAY_MULTIPLIER;
        maxRetries ??= DEFAULT_RETRIES;

        TOP:
        try
        {
            await f();
        }
        catch (Exception ex)
        {
            retries += 1;
            if (retries > maxRetries)
                throw;
            logger.LogWarning("(Retry {retries} of {maxRetries}), got exception {message}, waiting {wait}ms",
                retries, maxRetries, ex.Message, delay!.Value.TotalMilliseconds);
            await Task.Delay(delay.Value);
            delay = delay * multipler;
            goto TOP;
        }
    }

    public static async ValueTask<T> WithAutoRetryAllAsync<T>(ILogger logger, Func<ValueTask<T>> f,
        TimeSpan? delay = null, int? multipler = null, int? maxRetries = null)
    {
        var retries = 0;
        delay ??= DEFAULT_DELAY;
        multipler ??= DEFAULT_DELAY_MULTIPLIER;
        maxRetries ??= DEFAULT_RETRIES;

        TOP:
        try
        {
            return await f();
        }
        catch (Exception ex)
        {
            retries += 1;
            if (retries > maxRetries)
                throw;
            await Task.Delay(delay.Value);
            logger.LogWarning("(Retry {retries} of {maxRetries}), got exception {message}, waiting {wait}ms",
                retries, maxRetries, ex.Message, delay!.Value.TotalMilliseconds);
            delay = delay * multipler;
            goto TOP;
        }
    }

    public static void WithAutoRetry<TE>(ILogger logger, Action f, TimeSpan? delay = null, int? multipler = null,
        int? maxRetries = null) where TE : Exception
    {
        var retries = 0;
        delay ??= DEFAULT_DELAY;
        multipler ??= DEFAULT_DELAY_MULTIPLIER;
        maxRetries ??= DEFAULT_RETRIES;

        TOP:
        try
        {
            f();
        }
        catch (TE ex)
        {
            retries += 1;
            if (retries > maxRetries)
                throw;
            logger.LogWarning("(Retry {retries} of {maxRetries}), got exception {message}, waiting {wait}ms",
                retries, maxRetries, ex.Message, delay!.Value.TotalMilliseconds);
            Thread.Sleep(delay.Value);
            delay = delay * multipler;
            goto TOP;
        }
    }
}