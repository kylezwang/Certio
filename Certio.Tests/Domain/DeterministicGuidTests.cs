using Xunit;
using Certio.Domain.Identity;

namespace Certio.Tests.Domain
{
    /// <summary>
    /// Golden-value tests for DeterministicGuid.
    ///
    /// These GUIDs are already persisted in the Documents tables, so this function is part of the
    /// storage format rather than an implementation detail. The expected values below were derived
    /// independently from the algorithm (SHA-256 over "prefix:id", first 16 bytes, v4 version/variant
    /// bits,.NET's little-endian Guid layout) rather than by running the code under test, so they pin
    /// the format instead of merely restating the implementation.
    ///
    /// If one of these fails, the change is a breaking data migration, not a refactor. A different GUID
    /// does not throw - it simply matches no rows, so documents silently disappear from queries.
    /// </summary>
    public class DeterministicGuidTests
    {
        [Theory]
        [InlineData(1, "c7886fda-2d24-ad44-a639-c99d8161924a")]
        [InlineData(42, "9af46477-2688-c345-b80d-9d38b53ce003")]
        [InlineData(int.MaxValue, "63397312-51af-5441-bb82-907d150961e8")]
        public void ForOrganization_MatchesPersistedFormat(int organizationId, string expected)
        {
            Assert.Equal(Guid.Parse(expected), DeterministicGuid.ForOrganization(organizationId));
        }

        [Theory]
        [InlineData(1, "a0db3b0b-fa05-bf41-a050-efb3643e7aed")]
        [InlineData(12345, "03745a2b-c2dd-da49-8d7c-96d1fe4b9631")]
        public void ForUser_MatchesPersistedFormat(int userId, string expected)
        {
            Assert.Equal(Guid.Parse(expected), DeterministicGuid.ForUser(userId));
        }

        [Theory]
        [InlineData(1, "66f7ef72-8e04-f549-a6f1-4069b7c311b9")]
        [InlineData(999, "c81ce22d-fdc2-a64a-95e4-5402297762dd")]
        public void ForMatter_MatchesPersistedFormat(int matterId, string expected)
        {
            Assert.Equal(Guid.Parse(expected), DeterministicGuid.ForMatter(matterId));
        }

        [Fact]
        public void NamedHelpers_AgreeWithRawCreate()
        {
            // The six call sites being replaced all used Create-style calls with literal prefix strings.
            // This asserts the named helpers are exactly equivalent, so swapping them in is not a change
            // in behavior.
            Assert.Equal(DeterministicGuid.Create("certio:organization", 7), DeterministicGuid.ForOrganization(7));
            Assert.Equal(DeterministicGuid.Create("certio:user", 7), DeterministicGuid.ForUser(7));
            Assert.Equal(DeterministicGuid.Create("certio:matter", 7), DeterministicGuid.ForMatter(7));
        }

        [Fact]
        public void Create_IsStableAcrossCalls()
        {
            Assert.Equal(DeterministicGuid.ForMatter(500), DeterministicGuid.ForMatter(500));
        }

        [Fact]
        public void Create_DistinguishesNamespaces()
        {
            // Without the prefix, organization 1 and user 1 would collide.
            Assert.NotEqual(DeterministicGuid.ForOrganization(1), DeterministicGuid.ForUser(1));
            Assert.NotEqual(DeterministicGuid.ForUser(1), DeterministicGuid.ForMatter(1));
        }

        [Fact]
        public void Create_ProducesWellFormedVersion4Guid()
        {
            var bytes = DeterministicGuid.ForOrganization(1).ToByteArray();

            // ToByteArray round-trips the same layout the span constructor consumed, so the version and
            // variant bytes are at the indexes the implementation wrote: 6 and 8. Visible in the golden
            // value c7886fda-2d24-ad44-a639-...: the third group "ad44" is little-endian, so byte 6 is
            // 0x44, and the fourth group starts 0xa6.
            Assert.Equal(0x40, bytes[6] & 0xF0);
            Assert.Equal(0x80, bytes[8] & 0xC0);
        }
    }
}
