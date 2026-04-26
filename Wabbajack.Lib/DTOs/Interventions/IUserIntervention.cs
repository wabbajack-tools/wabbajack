using System;
using System.Threading;

namespace Wabbajack.DTOs.Interventions;

/// <summary>
/// Defines a message that requires user interaction. The user must perform some action
/// or make a choice. 
/// </summary>
public interface IUserIntervention
{
    /// <summary>
    /// The user didn't make a choice, so this action should be aborted
    /// </summary>
    void Cancel();

    /// <summary>
    /// Whether the interaction has been handled and no longer needs attention
    /// </summary>
    bool Handled { get; }
    
    /// <summary>
    /// Token that can be used to trigger cancellation when Cancel() is called.
    /// </summary>
    public CancellationToken Token { get; }

    void SetException(Exception exception);
}
