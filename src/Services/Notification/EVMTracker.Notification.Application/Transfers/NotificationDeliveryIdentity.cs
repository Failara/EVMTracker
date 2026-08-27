using System.Security.Cryptography;
using System.Text;

namespace EVMTracker.Notification.Application.Transfers;

public static class NotificationDeliveryIdentity
{
    public static Guid CreateTransferDeliveryId(Guid notificationId, string blockHash) =>
        Create($"transfer:{notificationId:N}:{blockHash.ToLowerInvariant()}");

    public static Guid CreateCorrectionDeliveryId(
        Guid notificationId,
        Guid orphanEventId) =>
        Create($"correction:{notificationId:N}:{orphanEventId:N}");

    private static Guid Create(string identity)
    {
        byte[] value = Encoding.UTF8.GetBytes(identity);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(value, hash);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash[..16]);
    }
}
