using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Common.Enums;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Services;

public sealed class UserService(
    SampleAnalysisTrackingDbContext db,
    IPasswordHasher<AppUser> passwordHasher)
{
    public async Task<PagedResult<UserListItem>> GetPagedAsync(
        UserListQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.Users
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchPattern = $"%{request.Search.Trim()}%";

            query = query.Where(user =>
                EF.Functions.ILike(user.Username, searchPattern)
                || EF.Functions.ILike(user.FullName, searchPattern)
                || EF.Functions.ILike(user.Email, searchPattern));
        }

        if (request.Role.HasValue)
        {
            var role = request.Role.Value;

            query = query.Where(user => user.Role == role);
        }

        if (request.IsActive.HasValue)
        {
            var isActive = request.IsActive.Value;

            query = query.Where(user => user.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new UserListItem(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<UserListItem>(
            items,
            request.Page,
            request.PageSize,
            totalCount);
    }

    public async Task<UserDetail?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserDetail(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.CreatedAt,
                user.UpdatedAt,
                user.CreatedSamples.Count(),
                user.AssignedSamples.Count()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserProfile?> GetProfileAsync(
        long id,
        CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserProfile(
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserItem>> GetLaboratoryWorkersAsync(
        CancellationToken cancellationToken)
    {
        return await db.Users
            .AsNoTracking()
            .Where(user =>
                user.Role == UserRole.Laboratory
                && user.IsActive)
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Select(user => new UserItem(
                user.Id,
                user.Username,
                user.Role.ToString(),
                user.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<UserOperationResult> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        return CreateUserAsync(
            username: request.Username,
            fullName: request.FullName,
            email: request.Email,
            password: request.Password,
            role: UserRole.Field,
            isActive: false,
            cancellationToken);
    }

    public Task<UserOperationResult> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        return CreateUserAsync(
            username: request.Username,
            fullName: request.FullName,
            email: request.Email,
            password: request.TemporaryPassword,
            role: request.Role,
            isActive: true,
            cancellationToken);
    }

    public async Task<UserOperationResult> UpdateAsync(
        long id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

        if (user is null)
        {
            return UserOperationResult.Failure(
                UserOperationError.NotFound);
        }

        var fullName = request.FullName.Trim();

        var email = NormalizeEmail(request.Email);

        if (await EmailExistsAsync(
            email,
            excludedUserId: id,
            cancellationToken))
        {
            return UserOperationResult.Failure(
                UserOperationError.EmailAlreadyExists);
        }

        var removesActiveAdminRole =
            user.IsActive
            && user.Role == UserRole.Admin
            && request.Role != UserRole.Admin;

        if (removesActiveAdminRole)
        {
            if (!await HasAnotherActiveAdminAsync(
                id,
                cancellationToken))
            {
                return UserOperationResult.Failure(
                    UserOperationError.CannotRemoveLastAdmin);
            }
        }

        user.FullName = fullName;
        user.Email = email;
        user.Role = request.Role;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success();
    }

    public async Task<UserOperationResult> UpdateProfileAsync(
        long id,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

        if (user is null)
        {
            return UserOperationResult.Failure(
                UserOperationError.NotFound);
        }

        var email = NormalizeEmail(request.Email);

        if (await EmailExistsAsync(
            email,
            excludedUserId: id,
            cancellationToken))
        {
            return UserOperationResult.Failure(
                UserOperationError.EmailAlreadyExists);
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success();
    }

    public async Task<UserOperationResult> ChangePasswordAsync(
        long id,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

        if (user is null)
        {
            return UserOperationResult.Failure(
                UserOperationError.NotFound);
        }

        var currentPasswordVerification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (currentPasswordVerification == PasswordVerificationResult.Failed)
        {
            return UserOperationResult.Failure(
                UserOperationError.CurrentPasswordIncorrect);
        }

        var newPasswordVerification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.NewPassword);

        if (newPasswordVerification != PasswordVerificationResult.Failed)
        {
            return UserOperationResult.Failure(
                UserOperationError.NewPasswordSameAsCurrent);
        }

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success();
    }

    public async Task<UserOperationResult> ChangeActiveStateAsync(
    long id,
    long currentUserId,
    ChangeUserActiveStateRequest request,
    CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(
            user => user.Id == id,
            cancellationToken);

        if (user is null)
        {
            return UserOperationResult.Failure(
                UserOperationError.NotFound);
        }

        if (!request.IsActive && user.Id == currentUserId)
        {
            return UserOperationResult.Failure(
                UserOperationError.CannotDeactivateSelf);
        }

        var removesActiveAdmin =
            user.IsActive
            && !request.IsActive
            && user.Role == UserRole.Admin;

        if (removesActiveAdmin)
        {
            if (!await HasAnotherActiveAdminAsync(
                id,
                cancellationToken))
            {
                return UserOperationResult.Failure(
                    UserOperationError.CannotRemoveLastAdmin);
            }
        }

        if (user.IsActive == request.IsActive)
        {
            return UserOperationResult.Success();
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return UserOperationResult.Success();
    }

    private async Task<UserOperationResult> CreateUserAsync(
        string username,
        string fullName,
        string email,
        string password,
        UserRole role,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var normalizedUsername = username
            .Trim()
            .ToLowerInvariant();

        var normalizedFullName = fullName.Trim();

        var normalizedEmail = NormalizeEmail(email);

        var usernameExists = await db.Users.AnyAsync(
            user => user.Username == normalizedUsername,
            cancellationToken);

        if (usernameExists)
        {
            return UserOperationResult.Failure(
                UserOperationError.UsernameAlreadyExists);
        }

        if (await EmailExistsAsync(
            normalizedEmail,
            excludedUserId: null,
            cancellationToken))
        {
            return UserOperationResult.Failure(
                UserOperationError.EmailAlreadyExists);
        }

        var now = DateTimeOffset.UtcNow;

        var user = new AppUser
        {
            Username = normalizedUsername,
            PasswordHash = string.Empty,
            FullName = normalizedFullName,
            Email = normalizedEmail,
            Role = role,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            password);

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.IsUniqueViolation())
        {
            return UserOperationResult.Failure(
                exception.GetConstraintName() == "IX_users_email"
                    ? UserOperationError.EmailAlreadyExists
                    : UserOperationError.UsernameAlreadyExists);
        }

        return UserOperationResult.Success(
            new UserCreated(
                user.Id,
                user.Username,
                user.Role,
                user.IsActive));
    }

    private Task<bool> HasAnotherActiveAdminAsync(
        long excludedUserId,
        CancellationToken cancellationToken)
    {
        return db.Users.AnyAsync(
            user =>
                user.Id != excludedUserId
                && user.Role == UserRole.Admin
                && user.IsActive,
            cancellationToken);
    }

    private Task<bool> EmailExistsAsync(
        string email,
        long? excludedUserId,
        CancellationToken cancellationToken)
    {
        return db.Users.AnyAsync(
            user =>
                user.Email == email
                && (!excludedUserId.HasValue
                    || user.Id != excludedUserId.Value),
            cancellationToken);
    }

    private static string NormalizeEmail(string email)
    {
        return email
            .Trim()
            .ToLowerInvariant();
    }
}
