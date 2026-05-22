using OS.Application.DTOs;

namespace OS.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginDto dto);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task ExtendSessionAsync();
    Task<bool> CheckSessionTimeoutAsync();
}
