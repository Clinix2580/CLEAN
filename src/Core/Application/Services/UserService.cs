using AutoMapper;
using FluentValidation;
using OS.Application.DTOs;
using OS.Application.Interfaces;
using OS.Domain.Entities;
using OS.Domain.Exceptions;
using BCryptNet = BCrypt.Net.BCrypt;
using IRuntimeAccessPolicy = OS.Domain.Interfaces.IRuntimeAccessPolicy;

namespace OS.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IValidator<CreateUserDto> _createValidator;
    private readonly IValidator<UpdateUserDto> _updateValidator;
    private readonly IAuditService _auditService;
    private readonly IRuntimeAccessPolicy _accessPolicy;

    public UserService(IUnitOfWork unitOfWork, IMapper mapper, IValidator<CreateUserDto> createValidator, IValidator<UpdateUserDto> updateValidator, IAuditService auditService, IRuntimeAccessPolicy accessPolicy)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _auditService = auditService;
        _accessPolicy = accessPolicy;
    }

    public async Task<UserDto?> GetByIdAsync(Guid id) => _mapper.Map<UserDto>(await _unitOfWork.Repository<User>().GetByIdAsync(id));
    public async Task<UserDto?> GetByUsernameAsync(string username) => _mapper.Map<UserDto>((await _unitOfWork.Repository<User>().FindAsync(u => u.Username == username)).FirstOrDefault());
    public async Task<IEnumerable<UserDto>> GetAllAsync() => _mapper.Map<IEnumerable<UserDto>>(await _unitOfWork.Repository<User>().GetAllAsync());

    public async Task<UserDto> CreateAsync(CreateUserDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var validation = await _createValidator.ValidateAsync(dto);
        if (!validation.IsValid) throw new OS.Domain.Exceptions.ValidationException(validation.Errors.Select(e => e.ErrorMessage).ToList());
        var user = _mapper.Map<User>(dto);
        user.PasswordHash = BCryptNet.HashPassword(dto.Password);
        user.CreatedAt = DateTime.UtcNow;
        await _unitOfWork.Repository<User>().AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("CREATE", nameof(User), user.Id.ToString(), null, null);
        return _mapper.Map<UserDto>(user);
    }

    public async Task UpdateAsync(UpdateUserDto dto)
    {
        _accessPolicy.DemandWriteAccess();
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(dto.Id) ?? throw new NotFoundException(nameof(User), dto.Id);
        var oldValues = System.Text.Json.JsonSerializer.Serialize(user);
        _mapper.Map(dto, user);
        // user.UpdatedAt = DateTime.UtcNow; // Comentado para evitar error CS1061 si la entidad no lo tiene
        await _unitOfWork.Repository<User>().UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("UPDATE", nameof(User), user.Id.ToString(), oldValues, null);
    }

    public async Task DeleteAsync(Guid id)
    {
        _accessPolicy.DemandWriteAccess();
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id) ?? throw new NotFoundException(nameof(User), id);
        await _unitOfWork.Repository<User>().DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();
        await _auditService.LogAsync("DELETE", nameof(User), id.ToString(), null, null);
    }

    public async Task<bool> ValidateCredentialsAsync(string username, string password)
    {
        var user = (await _unitOfWork.Repository<User>().FindAsync(u => u.Username == username && u.IsActive)).FirstOrDefault();
        return user != null && BCryptNet.Verify(password, user.PasswordHash);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        _accessPolicy.DemandWriteAccess();
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId) ?? throw new NotFoundException(nameof(User), userId);
        if (!BCryptNet.Verify(currentPassword, user.PasswordHash)) throw new UnauthorizedException();
        user.PasswordHash = BCryptNet.HashPassword(newPassword);
        await _unitOfWork.Repository<User>().UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();
    }
}

