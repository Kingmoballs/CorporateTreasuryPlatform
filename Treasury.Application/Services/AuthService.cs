using BCrypt.Net;
using Treasury.Application.DTOs.Auth;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;
using Treasury.Application.Common.Exceptions;

namespace Treasury.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository
        _userRepository;

    private readonly IJwtService
        _jwtService;
    
    private readonly IRoleRepository
        _roleRepository;

    private readonly IOrganizationRepository
        _organizationRepository;

    public AuthService(
        IUserRepository userRepository,
        IJwtService jwtService,
        IRoleRepository roleRepository,
        IOrganizationRepository
            organizationRepository)
    {
        _userRepository = userRepository;

        _jwtService = jwtService;

        _roleRepository = roleRepository;

        _organizationRepository =
            organizationRepository;
    }

    public async Task<AuthResponseDto>
        Register(RegisterDto dto)
    {
        var existingUser =
            await _userRepository
                .GetByEmail(dto.Email);

        if(existingUser != null)
        {
            throw new ConflictException(
                "Email already exists");
        }

        var role =
            await _roleRepository
                .GetByName(Roles.TreasuryOfficer);

        if(role == null)
        {
            throw new ResourceNotFoundException(
                "Default role not found");
        }

        var organization =
            await _organizationRepository
                .GetByCode(
                    OrganizationDefaults
                        .OrganizationCode);

        if (organization == null)
        {
            throw new ResourceNotFoundException(
                "Default organization not found");
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

        var membership =
            new OrganizationMembership
            {
                Id = Guid.NewGuid(),
                OrganizationId =
                    organization.Id,
                Organization = organization,
                UserId = user.Id,
                User = user,
                RoleId = role.Id,
                Role = role,
                IsActive = true,
                IsDefault = true
            };

        user.OrganizationMemberships.Add(
            membership);

        await _userRepository.Add(user);

        await _userRepository.SaveChanges();

        var token =
            _jwtService.GenerateToken(user);

        var currentMembership =
            GetCurrentMembership(user);

        return new AuthResponseDto
        {
            AccessToken = token,

            Email = user.Email,

            Role =
                currentMembership?.Role.Name ??
                user.Role.Name,

            OrganizationId =
                currentMembership?.OrganizationId,

            OrganizationCode =
                currentMembership?
                    .Organization.Code ??
                string.Empty
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
            throw new UnauthorizedAccessException(
                "Invalid credentials");
        }

        var validPassword =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if(!validPassword)
        {
            throw new UnauthorizedAccessException(
                "Invalid credentials");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException(
                "This user account is inactive.");
        }

        var token =
            _jwtService.GenerateToken(user);

        var currentMembership =
            GetCurrentMembership(user);

        return new AuthResponseDto
        {
            AccessToken = token,

            Email = user.Email,

            Role =
                currentMembership?.Role.Name ??
                user.Role.Name,

            OrganizationId =
                currentMembership?.OrganizationId,

            OrganizationCode =
                currentMembership?
                    .Organization.Code ??
                string.Empty
        };
    }

    /*
     * The default active membership determines the tenant
     * and role represented by the issued access token.
     */
    private static OrganizationMembership?
        GetCurrentMembership(User user)
    {
        return user.OrganizationMemberships
            .Where(membership =>
                membership.IsActive &&
                membership.Organization.IsActive)
            .OrderByDescending(membership =>
                membership.IsDefault)
            .ThenBy(membership =>
                membership.JoinedAtUtc)
            .FirstOrDefault();
    }
}
