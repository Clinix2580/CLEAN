using OS.Application.DTOs;

namespace OS.Application.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserDto?> GetByUsernameAsync(string username);
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto> CreateAsync(CreateUserDto dto);
    Task UpdateAsync(UpdateUserDto dto);
    Task DeleteAsync(Guid id);
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
