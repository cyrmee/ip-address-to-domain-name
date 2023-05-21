using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace ip_address_to_domain_name;

internal abstract class Program
{
    private static void Main()
    {
        const string jsonFilePath = "D:\\ip-range.json";
        var json = File.ReadAllText(jsonFilePath);

        // Deserialize JSON data
        var data = JsonConvert.DeserializeObject<Root>(json);
        var ipAddresses = new List<string>();

        foreach (var prefix in data!.Prefixes!)
        {
            var ipAddressWithSubnetMask = prefix.IPv4Prefix;
            var ipAddress = IPAddress.Parse(ipAddressWithSubnetMask!.Split('/')[0]);
            var subnetMaskLength = int.Parse(ipAddressWithSubnetMask.Split('/')[1]);
            var subnetMask = GetSubnetMask(subnetMaskLength);
            var networkAddress = GetNetworkAddress(ipAddress, subnetMask);
            var broadcastAddress = GetBroadcastAddress(ipAddress, subnetMask);

            while (!networkAddress.Equals(broadcastAddress))
            {
                ipAddresses.Add(networkAddress.ToString());

                var bytes = networkAddress.GetAddressBytes();

                // Increment IP address
                for (var i = 3; i >= 0; i--)
                {
                    if (bytes[i] == 255)
                    {
                        bytes[i] = 0;
                    }
                    else
                    {
                        bytes[i]++;
                        break;
                    }
                }

                networkAddress = new IPAddress(bytes);
            }
        }

        ProcessIpAddresses(ipAddresses);
    }

    private static void ProcessIpAddresses(List<string> ipAddresses)
    {
        var count = 0;

        try
        {
            using (new StreamWriter("D:\\ipaddresses.txt", false)
                   {
                       NewLine = null,
                       AutoFlush = false
                   })
            {
                Console.WriteLine("File content cleared.");
            }

            Console.WriteLine("Processing IP addresses...");

            using (var writer = new StreamWriter("D:\\ipaddresses.txt", true)
                   {
                       NewLine = null,
                       AutoFlush = false
                   })
            {
                var tasks = new List<Task>();

                foreach (var ip in ipAddresses)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            var host = Dns.GetHostEntry(ip);
                            Console.WriteLine($"IP: {ip} - Domain: {host.HostName}");
                            // ReSharper disable once AccessToDisposedClosure
                            writer.WriteLine($"IP: {ip} - Domain: {host.HostName}");
                        }
                        catch (SocketException)
                        {
                            Console.WriteLine($"IP: {ip} - No domain found");
                        }
                    }));
                    count++;
                }

                Task.WaitAll(tasks.ToArray());
            }

            Console.WriteLine("Content successfully written to the file.");
            Console.WriteLine($"Count = {count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }

    private static IPAddress GetSubnetMask(int subnetMaskLength)
    {
        var significantBits = 0xffffffff << (32 - subnetMaskLength);
        byte[] maskBytes = BitConverter.GetBytes(significantBits);
        Array.Reverse(maskBytes);
        return new IPAddress(maskBytes);
    }

    private static IPAddress GetNetworkAddress(IPAddress ipAddress, IPAddress subnetMask)
    {
        var ipBytes = ipAddress.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        var networkBytes = new byte[ipBytes.Length];

        for (var i = 0; i < ipBytes.Length; i++)
        {
            networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
        }

        return new IPAddress(networkBytes);
    }

    private static IPAddress GetBroadcastAddress(IPAddress ipAddress, IPAddress subnetMask)
    {
        var ipBytes = ipAddress.GetAddressBytes();
        var maskBytes = subnetMask.GetAddressBytes();
        var broadcastBytes = new byte[ipBytes.Length];

        for (var i = 0; i < ipBytes.Length; i++)
        {
            broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
        }

        return new IPAddress(broadcastBytes);
    }
}

internal class Root
{
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public Prefix[]? Prefixes { get; set; }
}

// ReSharper disable once ClassNeverInstantiated.Global
internal record Prefix
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string? IPv4Prefix { get; set; }
}