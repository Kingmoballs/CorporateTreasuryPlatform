using BCrypt.Net;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository
        _userRepository;

    private readonly IJwtService
        _jwtService;
    
    private readonly IRoleRepository
        _roleRepository;

    public AuthService(
        IUserRepository userRepository,
        IJwtService jwtService,
        IRoleRepository roleRepository)
    {
        _userRepository = userRepository;

        _jwtService = jwtService;

        _roleRepository = roleRepository;
    }

    public async Task<AuthResponseDto>
        Register(RegisterDto dto)
    {
        var existingUser =
            await _userRepository
                .GetByEmail(dto.Email);

        if(existingUser != null)
        {
            throw new Exception(
                "Email already exists");
        }

        var role =
            await _roleRepository
                .GetByName(Roles.TreasuryOfficer);

        if(role == null)
        {
            throw new Exception(
                "Default role not found");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),

            FirstName = dto.FirstName,

            LastName = dto.LastName,

            Email = dto.Email,

            PasswordHash = BCrypt.Net.BCrypt
                .HashPassword(dto.Password),

            RoleId = role.Id,

            Role = role
        };

        await _userRepository.Add(user);

        await _userRepository.SaveChanges();

        var token =
            _jwtService.GenerateToken(user);

        return new AuthResponseDto
        {
            AccessToken = token,

            Email = user.Email,

            Role = user.Role.Name
        };
    }

    public async Task<AuthResponseDto>
        Login(LoginDto dto)
    {
        var user =
            await _userRepository
                .GetByEmail(dto.Email);

        if(user == null)
        {
            throw new Exception(
                "Invalid credentials");
        }

        var validPassword =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if(!validPassword)
        {
            throw new Exception(
                "Invalid credentials");
        }

        if (!user.IsActive)
        {
            throw new Exception(
                "This user account is inactive.");
        }

        var token =
            _jwtService.GenerateToken(user);

        return new AuthResponseDto
        {
            AccessToken = token,

            Email = user.Email,

            Role = user.Role.Name
        };
    }
}