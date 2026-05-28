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

    public AuthService(
        IUserRepository userRepository,
        IJwtService jwtService)
    {
        _userRepository = userRepository;

        _jwtService = jwtService;
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

        var user = new User
        {
            Id = Guid.NewGuid(),

            FirstName = dto.FirstName,

            LastName = dto.LastName,

            Email = dto.Email,

            PasswordHash = BCrypt.Net.BCrypt
                .HashPassword(dto.Password),

            Role = new Role
            {
                Name = Roles.TreasuryOfficer
            }
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