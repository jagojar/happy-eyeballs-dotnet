using System.Net;
using System.Net.Sockets;

namespace HappyEyeballs;

/// <summary>
/// Sorts IP addresses according to RFC 6724 (Destination Address Selection).
/// </summary>
public static class AddressSorter
{
    /// <summary>
    /// Sorts addresses following RFC 6724 rules with Happy Eyeballs v2 modifications.
    /// IPv6 addresses are preferred over IPv4 by default.
    /// </summary>
    public static IReadOnlyList<IPAddress> Sort(IEnumerable<IPAddress> addresses, bool preferIPv6 = true)
    {
        var addressList = addresses.ToList();
        
        // Group by address family
        var ipv6Addresses = addressList.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToList();
        var ipv4Addresses = addressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();

        // Sort each group
        ipv6Addresses = SortIPv6Addresses(ipv6Addresses);
        ipv4Addresses = SortIPv4Addresses(ipv4Addresses);

        // Interleave addresses per RFC 8305:
        // "The client SHOULD prefer the first IP address family returned by the host's
        // address preference policy, with the caveat that IPv6 should be preferred over IPv4."
        if (preferIPv6)
        {
            return InterleaveAddresses(ipv6Addresses, ipv4Addresses);
        }
        else
        {
            return InterleaveAddresses(ipv4Addresses, ipv6Addresses);
        }
    }

    private static List<IPAddress> SortIPv6Addresses(List<IPAddress> addresses)
    {
        // Sort IPv6 addresses by scope and preference
        return addresses
            .OrderBy(a => GetIPv6Priority(a))
            .ThenBy(a => a.ScopeId)
            .ToList();
    }

    private static List<IPAddress> SortIPv4Addresses(List<IPAddress> addresses)
    {
        // Sort IPv4 addresses
        return addresses
            .OrderBy(a => GetIPv4Priority(a))
            .ToList();
    }

    private static int GetIPv6Priority(IPAddress address)
    {
        // Lower number = higher priority
        if (address.IsIPv6Teredo) return 5;
        if (address.IsIPv6LinkLocal) return 4;
        if (address.IsIPv6SiteLocal) return 3;
        if (address.IsIPv6UniqueLocal) return 2;
        return 1; // Global unicast
    }

    private static int GetIPv4Priority(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        
        // Private addresses
        if (bytes[0] == 10 || 
            (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
            (bytes[0] == 192 && bytes[1] == 168))
        {
            return 2;
        }

        // Loopback
        if (bytes[0] == 127)
        {
            return 3;
        }

        return 1; // Public addresses
    }

    /// <summary>
    /// Interleaves two lists of addresses, preferring the first list.
    /// Per RFC 8305: alternate between preferred and alternate address families.
    /// </summary>
    private static IReadOnlyList<IPAddress> InterleaveAddresses(
        List<IPAddress> preferredAddresses,
        List<IPAddress> alternateAddresses)
    {
        var result = new List<IPAddress>();
        int maxCount = Math.Max(preferredAddresses.Count, alternateAddresses.Count);

        for (int i = 0; i < maxCount; i++)
        {
            if (i < preferredAddresses.Count)
            {
                result.Add(preferredAddresses[i]);
            }
            if (i < alternateAddresses.Count)
            {
                result.Add(alternateAddresses[i]);
            }
        }

        return result;
    }
}
