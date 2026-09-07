using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SampleAnalysisTracking.Data;
using SampleAnalysisTracking.DTOs;
using SampleAnalysisTracking.Models;

namespace SampleAnalysisTracking.Services;

public sealed class AuthService(
    SampleAnalysisTrackingDbContext db,
    IPasswordHasher<AppUser> passwordHasher)
{
    public async Task<AuthUser?> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username
            .Trim()
            .ToLowerInvariant();

        var user = await db.Users.SingleOrDefaultAsync(
            user => user.Username == username,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var verification = VerifyPassword(
            user,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(
                user,
                request.Password);
            user.UpdatedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
        }

        return new AuthUser(
            user.Id,
            user.Username,
            user.Role.ToString());
    }

    private PasswordVerificationResult VerifyPassword(
        AppUser user,
        string password)
    {
        try
        {
            return passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                password);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }
}
