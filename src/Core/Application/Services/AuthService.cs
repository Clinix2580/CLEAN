using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using IRuntimeAccessPolicy = OS.Domain.Interfaces.IRuntimeAccessPolicy;
using BCryptNet = BCrypt.Net.BCrypt;

namespace OS.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IComplianceManager _complianceManager;
    private readonly IRuntimeAccessPolicy _accessPolicy;
    private DateTime _lastActivity;
    private bool _isAuthenticated;
    private UserDto? _currentUser;

    public AuthService(IUnitOfWork unitOfWork, IAuditService auditService, IComplianceManager complianceManager,
        IRuntimeAccessPolicy accessPolicy)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _complianceManager = complianceManager;
        _accessPolicy = accessPolicy;
        _lastActivity = DateTime.UtcNow;
    }

    public async Task<AuthResultDto> LoginAsync(LoginDto dto)
    {
#if DEMO_VERSION
        if (string.Equals(dto.Username, "demo.admin", StringComparison.OrdinalIgnoreCase)
            && dto.Password == "Demo123!")
        {
            _isAuthenticated = true;
            _currentUser = new UserDto
            {
                Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Username = "demo.admin",
                Email = "demo.admin@clinicos.local",
                Role = "DemoAdmin",
                Permissions = ["Read"]
            };
            _lastActivity = DateTime.UtcNow;

            await _auditService.LogAsync("LOGIN_SUCCESS", "User", _currentUser.Id.ToString(), null, "Demo read-only login");

            return new AuthResultDto
            {
                Success = true,
                User = _currentUser,
                ExpiresAt = DateTime.UtcNow.AddHours(8)
            };
        }
#endif

        var users = await _unitOfWork.Repository<User>()
            .FindAsync(u => u.Username == dto.Username && u.IsActive);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            await _auditService.LogAsync("LOGIN_FAILED", "User", null, null, $"Username: {dto.Username}");
            return new AuthResultDto { Success = false, Error = "Invalid credentials" };
        }

        if (!BCryptNet.Verify(dto.Password, user.PasswordHash))
        {
            await _auditService.LogAsync("LOGIN_FAILED", "User", user.Id.ToString(), null, null);
            return new AuthResultDto { Success = false, Error = "Invalid credentials" };
        }

        _isAuthenticated = true;
        _currentUser = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            Permissions = user.Permissions
        };

        if (!_accessPolicy.IsReadOnly)
        {
            user.LastLogin = DateTime.UtcNow;
            await _unitOfWork.Repository<User>().UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
        }

        _lastActivity = DateTime.UtcNow;

        await _auditService.LogAsync("LOGIN_SUCCESS", "User", user.Id.ToString(), null, null);

        var expiresAt = DateTime.UtcNow.AddHours(8);
        return new AuthResultDto
        {
            Success = true,
            User = _currentUser,
            ExpiresAt = expiresAt
        };
    }

    public async Task LogoutAsync()
    {
        if (_currentUser != null)
        {
            await _auditService.LogAsync("LOGOUT", "User", _currentUser.Id.ToString(), null, null);
        }

        _isAuthenticated = false;
        _currentUser = null;
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_isAuthenticated);
    }

    public Task ExtendSessionAsync()
    {
        _lastActivity = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> CheckSessionTimeoutAsync()
    {
        var timeoutMinutes = _complianceManager.AutoLogoutMinutes;
        var inactiveDuration = DateTime.UtcNow - _lastActivity;
        var isTimedOut = inactiveDuration.TotalMinutes > timeoutMinutes;

        if (isTimedOut && _isAuthenticated)
        {
            _isAuthenticated = false;
        }

        return Task.FromResult(isTimedOut);
    }
}

