using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Certio.Domain.Identity
{
    /// <summary>
    /// Bridges the two identity schemes in this system. Core entities use <c>int</c> primary keys; the
    /// document subsystem and the AI service use <c>Guid</c>. This maps an int entity ID onto a stable,
    /// reproducible Guid so both sides can refer to the same organization, user, or matter without
    /// storing a mapping table.
    ///
    /// The output is a name-based UUID: SHA-256 over "prefix:id", truncated to 16 bytes, with the version
    /// and variant bits set so the result is a well-formed v4-shaped Guid.
    ///
    /// IMPORTANT: this function is effectively part of the persisted schema. Documents in the database
    /// already carry GUIDs produced by it. Changing the prefix strings, the hash algorithm, the byte
    /// count, or the number formatting will produce different GUIDs and silently orphan existing rows -
    /// queries will return nothing rather than fail. Do not "clean up" the implementation.
    ///
    /// This logic previously existed as six independent private copies (AIAgentService,
    /// UserDataContextService, ClientController, DriveOAuthController, DocumentsController,
    /// DocumentsApiController) that all had to agree forever.
    /// </summary>
    public static class DeterministicGuid
    {
        /// <summary>Namespace prefixes. These strings are part of the persisted format.</summary>
        public static class Namespaces
        {
            public const string Organization = "certio:organization";
            public const string User = "certio:user";
            public const string Matter = "certio:matter";
        }

        public static Guid ForOrganization(int organizationId) => Create(Namespaces.Organization, organizationId);

        public static Guid ForUser(int userId) => Create(Namespaces.User, userId);

        public static Guid ForMatter(int matterId) => Create(Namespaces.Matter, matterId);

        /// <summary>
        /// Creates a deterministic Guid from a namespace prefix and an integer ID. Prefer the named
        /// helpers above; they remove the chance of a typo in the prefix, which would silently produce a
        /// Guid that matches nothing.
        /// </summary>
        public static Guid Create(string namespacePrefix, int value)
        {
            var input = $"{namespacePrefix}:{value.ToString(CultureInfo.InvariantCulture)}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

            Span<byte> guidBytes = stackalloc byte[16];
            hash.AsSpan(0, 16).CopyTo(guidBytes);
            guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x40); // Version 4
            guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80); // Variant RFC 4122

            return new Guid(guidBytes);
        }
    }
}
