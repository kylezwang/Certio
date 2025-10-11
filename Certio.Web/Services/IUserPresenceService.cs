namespace Certio.Web.Services;

public interface IUserPresenceService
{
    void SetUserOnline(int userId, string connectionId, int organizationId);
    void SetUserOffline(string connectionId);
    bool IsUserOnline(int userId);
    List<int> GetOnlineUsersInOrganization(int organizationId);
    Dictionary<int, DateTime> GetUserLastSeenTimes(List<int> userIds);
}

