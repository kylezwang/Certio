using System.Security.Cryptography;
using System.Text;
using Certio.Domain.Organizations;
using Certio.Domain.Users;
using Certio.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Certio.Web.Services
{
    public interface IJoinCodeService
    {
        Task<OrganizationJoinCode> GenerateAsync(int organizationId, int createdByUserId, UserType invitedUserType, OrganizationRole invitedRole, string? teamName, int maxUses, TimeSpan ttl, CancellationToken ct = default);
        Task<OrganizationJoinCode?> GetValidAsync(string code, CancellationToken ct = default);
        Task<bool> ConsumeAsync(string code, CancellationToken ct = default);
    }

    public class JoinCodeService : IJoinCodeService
    {
        private readonly ApplicationDbContext _db;

        public JoinCodeService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<OrganizationJoinCode> GenerateAsync(
            int organizationId,
            int createdByUserId,
            UserType invitedUserType,
            OrganizationRole invitedRole,
            string? teamName,
            int maxUses,
            TimeSpan ttl,
            CancellationToken ct = default)
        {
            var code = await GenerateUniqueCodeAsync(ct);

            var joinCode = new OrganizationJoinCode
            {
                OrganizationId = organizationId,
                CreatedByUserId = createdByUserId,
                Code = code,
                InvitedUserType = invitedUserType,
                InvitedRole = invitedRole,
                TeamName = teamName,
                MaxUses = Math.Max(1, maxUses),
                UsesRemaining = Math.Max(1, maxUses),
                ExpiresAt = DateTime.UtcNow.Add(ttl),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.OrganizationJoinCodes.Add(joinCode);
            await _db.SaveChangesAsync(ct);
            return joinCode;
        }

        public async Task<OrganizationJoinCode?> GetValidAsync(string code, CancellationToken ct = default)
        {
            var join = await _db.OrganizationJoinCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Code == code, ct);

            if (join == null) return null;
            if (!join.IsActive) return null;
            if (join.ExpiresAt <= DateTime.UtcNow) return null;
            if (join.UsesRemaining <= 0) return null;

            return join;
        }

        public async Task<bool> ConsumeAsync(string code, CancellationToken ct = default)
        {
            var join = await _db.OrganizationJoinCodes.FirstOrDefaultAsync(j => j.Code == code, ct);
            if (join == null) return false;
            if (!join.IsActive) return false;
            if (join.ExpiresAt <= DateTime.UtcNow) return false;
            if (join.UsesRemaining <= 0) return false;

            join.UsesRemaining -= 1;
            join.LastUsedAt = DateTime.UtcNow;
            if (join.UsesRemaining <= 0)
            {
                join.IsActive = false;
            }
            await _db.SaveChangesAsync(ct);
            return true;
        }

        private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
        {
            // 10-char URL-safe code
            while (true)
            {
                var code = GenerateCode(10);
                var exists = await _db.OrganizationJoinCodes.AnyAsync(j => j.Code == code, ct);
                if (!exists) return code;
            }
        }

        private static string GenerateCode(int length)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // avoid ambiguous chars
            var bytes = RandomNumberGenerator.GetBytes(length);
            var sb = new StringBuilder(length);
            foreach (var b in bytes)
            {
                sb.Append(alphabet[b % alphabet.Length]);
            }
            return sb.ToString();
        }
    }
}


