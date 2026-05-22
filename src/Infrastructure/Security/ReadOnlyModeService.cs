using OS.Application.Interfaces;
using OS.Domain.Interfaces;

namespace OS.Infrastructure.Security;

public sealed class ReadOnlyModeService : IReadOnlyModeService
{
    private readonly IRuntimeAccessPolicy _runtimeAccessPolicy;

    public ReadOnlyModeService(IRuntimeAccessPolicy runtimeAccessPolicy)
    {
        _runtimeAccessPolicy = runtimeAccessPolicy;
    }

    public bool IsReadOnlyMode => _runtimeAccessPolicy.IsReadOnly;

    public string Reason => _runtimeAccessPolicy.Reason;

    public event EventHandler? StateChanged;

    public void Refresh()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
