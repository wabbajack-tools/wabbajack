using System;

namespace Wabbajack.App.Interfaces;

public interface IScreenView
{
    public Type ViewModelType { get; }
    public string HumanName { get; }
}