using System.Collections.Concurrent;

namespace Certio.Web.Services;

public class UserPresenceService : IUserPresenceService
{
    private static readonly ConcurrentDictionary<string, UserPresenceInfo> _connectionToUser = new();
    private static readonly ConcurrentDictionary<int, HashSet<string>> _userToConnections = new();
    private static readonly ConcurrentDictionary<int, DateTime> _userLastSeen = new();

    public void SetUserOnline(int userId, string connectionId, int organizationId)
    {
        // Add connection info
        _connectionToUser[connectionId] = new UserPresenceInfo
        {
            UserId = userId,
            OrganizationId = organizationId,
            ConnectedAt = DateTime.UtcNow
        };

        // Add to user connections
        _userToConnections.AddOrUpdate(userId,
            new HashSet<string> { connectionId },
            (_, connections) =>
            {
                lock (connections)
                {
                    connections.Add(connectionId);
                }
                return connections;
            });

        // Update last seen
        _userLastSeen[userId] = DateTime.UtcNow;
    }

    public void SetUserOffline(string connectionId)
    {
        if (_connectionToUser.TryRemove(connectionId, out var userInfo))
        {
            // Remove from user connections
            if (_userToConnections.TryGetValue(userInfo.UserId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(connectionId);
                    
                    // If no more connections, update last seen
                    if (connections.Count == 0)
                    {
                        _userLastSeen[userInfo.UserId] = DateTime.UtcNow;
                    }
                }
            }
        }
    }

    public bool IsUserOnline(int userId)
    {
        if (_userToConnections.TryGetValue(userId, out var connections))
        {
            lock (connections)
            {
                return connections.Count > 0;
            }
        }
        return false;
    }

    public List<int> GetOnlineUsersInOrganization(int organizationId)
    {
        return _connectionToUser.Values
            .Where(info => info.OrganizationId == organizationId)
            .Select(info => info.UserId)
            .Distinct()
            .ToList();
    }

    public Dictionary<int, DateTime> GetUserLastSeenTimes(List<int> userIds)
    {
        var result = new Dictionary<int, DateTime>();
        foreach (var userId in userIds)
        {
            if (_userLastSeen.TryGetValue(userId, out var lastSeen))
            {
                result[userId] = lastSeen;
            }
        }
        return result;
    }
}

public class UserPresenceInfo
{
    public int UserId { get; set; }
    public int OrganizationId { get; set; }
    public DateTime ConnectedAt { get; set; }
}

