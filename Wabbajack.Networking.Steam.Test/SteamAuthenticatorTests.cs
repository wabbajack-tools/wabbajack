using System;
using System.Threading;
using System.Threading.Tasks;
using Wabbajack.DTOs.Interventions;
using Wabbajack.Networking.Steam.UserInterventions;
using Xunit;

namespace Wabbajack.Networking.Steam.Test;

public class SteamAuthenticatorTests
{
    [Fact]
    public async Task PassesADeviceCodeStraightThrough()
    {
        var prompt = new FakePrompt {DeviceCode = "12345"};
        var authenticator = new SteamAuthenticator(prompt, CancellationToken.None);

        Assert.Equal("12345", await authenticator.GetDeviceCodeAsync(false));
    }

    [Fact]
    public async Task PassesAnEmailCodeStraightThrough()
    {
        var prompt = new FakePrompt {EmailCode = "ABCDE"};
        var authenticator = new SteamAuthenticator(prompt, CancellationToken.None);

        Assert.Equal("ABCDE", await authenticator.GetEmailCodeAsync("someone@example.com", false));
        Assert.Equal("someone@example.com", prompt.LastEmail);
    }

    [Fact]
    public async Task TellsThePromptWhenTheLastCodeWasRejected()
    {
        var prompt = new FakePrompt {DeviceCode = "12345"};
        var authenticator = new SteamAuthenticator(prompt, CancellationToken.None);

        await authenticator.GetDeviceCodeAsync(true);

        Assert.True(prompt.LastPreviousCodeWasIncorrect);
    }

    [Fact]
    public async Task ANullDeviceCodeCancelsTheLogin()
    {
        // SteamKit's retry loop around this call is infinite and throws on a null code, so cancellation has
        // to be raised from inside the authenticator or a cancelled prompt simply asks again forever.
        var authenticator = new SteamAuthenticator(new FakePrompt {DeviceCode = null}, CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authenticator.GetDeviceCodeAsync(false));
    }

    [Fact]
    public async Task ANullEmailCodeCancelsTheLogin()
    {
        var authenticator = new SteamAuthenticator(new FakePrompt {EmailCode = null}, CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authenticator.GetEmailCodeAsync("someone@example.com", false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyCodeCancelsTheLogin(string code)
    {
        var authenticator = new SteamAuthenticator(new FakePrompt {DeviceCode = code}, CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authenticator.GetDeviceCodeAsync(false));
    }

    [Fact]
    public async Task AnAlreadyCancelledTokenNeverReachesThePrompt()
    {
        var prompt = new FakePrompt {DeviceCode = "12345"};
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var authenticator = new SteamAuthenticator(prompt, source.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authenticator.GetDeviceCodeAsync(false));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            authenticator.GetEmailCodeAsync("someone@example.com", false));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => authenticator.AcceptDeviceConfirmationAsync());

        Assert.False(prompt.WasAsked);
    }

    [Fact]
    public async Task CancellingWhileWaitingUnblocksThePrompt()
    {
        // The prompt is sitting on a human. Cancelling has to end the wait, not leave the polling loop
        // parked on a task that will never complete.
        using var source = new CancellationTokenSource();
        var prompt = new BlockingPrompt();
        var authenticator = new SteamAuthenticator(prompt, source.Token);

        var pending = authenticator.GetDeviceCodeAsync(false);
        await prompt.Entered.Task;
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task DeviceConfirmationIsHandedToThePrompt()
    {
        Assert.True(await new SteamAuthenticator(new FakePrompt {AcceptDeviceConfirmation = true},
            CancellationToken.None).AcceptDeviceConfirmationAsync());

        Assert.False(await new SteamAuthenticator(new FakePrompt {AcceptDeviceConfirmation = false},
            CancellationToken.None).AcceptDeviceConfirmationAsync());
    }

    [Fact]
    public async Task TheInterventionPromptReturnsNullWhenTheUserCancels()
    {
        var handler = new CapturingInterventionHandler();
        var prompt = new InterventionSteamGuardPrompt(handler);

        var pending = prompt.GetDeviceCodeAsync(false, CancellationToken.None);
        var intervention = await handler.Raised.Task;
        intervention.Cancel();

        Assert.Null(await pending);
    }

    [Fact]
    public async Task TheInterventionPromptReturnsTheCodeTheUserGave()
    {
        var handler = new CapturingInterventionHandler();
        var prompt = new InterventionSteamGuardPrompt(handler);

        var pending = prompt.GetEmailCodeAsync("someone@example.com", true, CancellationToken.None);
        var intervention = (GetAuthCode) await handler.Raised.Task;

        Assert.Equal(GetAuthCode.AuthType.EmailCode, intervention.Type);
        Assert.Equal("someone@example.com", intervention.Email);
        Assert.True(intervention.PreviousCodeWasIncorrect);

        intervention.Finish("54321");
        Assert.Equal("54321", await pending);
    }

    [Fact]
    public async Task TheInterventionPromptReturnsNullWhenTheCallerCancels()
    {
        var handler = new CapturingInterventionHandler();
        var prompt = new InterventionSteamGuardPrompt(handler);

        using var source = new CancellationTokenSource();
        var pending = prompt.GetDeviceCodeAsync(false, source.Token);
        await handler.Raised.Task;
        await source.CancelAsync();

        Assert.Null(await pending);
    }

    private class FakePrompt : ISteamGuardPrompt
    {
        public string? DeviceCode { get; init; }
        public string? EmailCode { get; init; }
        public bool AcceptDeviceConfirmation { get; init; } = true;
        public bool WasAsked { get; private set; }
        public string? LastEmail { get; private set; }
        public bool LastPreviousCodeWasIncorrect { get; private set; }

        public Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token)
        {
            WasAsked = true;
            LastPreviousCodeWasIncorrect = previousCodeWasIncorrect;
            return Task.FromResult(DeviceCode);
        }

        public Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token)
        {
            WasAsked = true;
            LastEmail = email;
            LastPreviousCodeWasIncorrect = previousCodeWasIncorrect;
            return Task.FromResult(EmailCode);
        }

        public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
        {
            WasAsked = true;
            return Task.FromResult(AcceptDeviceConfirmation);
        }
    }

    private class BlockingPrompt : ISteamGuardPrompt
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string?> GetDeviceCodeAsync(bool previousCodeWasIncorrect, CancellationToken token)
        {
            Entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return null;
        }

        public Task<string?> GetEmailCodeAsync(string email, bool previousCodeWasIncorrect, CancellationToken token)
        {
            return GetDeviceCodeAsync(previousCodeWasIncorrect, token);
        }

        public Task<bool> AcceptDeviceConfirmationAsync(CancellationToken token)
        {
            return Task.FromResult(true);
        }
    }

    private class CapturingInterventionHandler : IUserInterventionHandler
    {
        public TaskCompletionSource<IUserIntervention> Raised { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Raise(IUserIntervention intervention)
        {
            Raised.TrySetResult(intervention);
        }
    }
}
