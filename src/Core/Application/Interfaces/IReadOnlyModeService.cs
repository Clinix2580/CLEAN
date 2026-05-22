namespace OS.Application.Interfaces;

public interface IReadOnlyModeService
{
    bool IsReadOnlyMode { get; }
    string Reason { get; }
    event EventHandler? StateChanged;
    void Refresh();
}
