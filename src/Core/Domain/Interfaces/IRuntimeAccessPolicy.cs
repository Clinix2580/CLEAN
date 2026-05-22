namespace OS.Domain.Interfaces;

public interface IRuntimeAccessPolicy
{
    bool IsReadOnly { get; }
    string Reason { get; }
    void DemandWriteAccess();
}
