using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RoboSync.App.Mvvm;

/// <summary>
/// Minimal INotifyPropertyChanged base class. Hand-rolled so the application has no
/// third-party runtime dependencies and stays trivial to build and run.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
