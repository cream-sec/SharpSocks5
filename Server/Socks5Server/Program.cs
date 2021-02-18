using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Socks5Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string IPAddress = null;
            string Socks5Port = string.Empty;
            string WebHostPort = string.Empty;
            string User = string.Empty;
            string Password = string.Empty;
            string Identifier = "socks";

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine(@"");
            Console.WriteLine(@"     _______.  ______     ______  __  ___      _______.   _____  ");
            Console.WriteLine(@"    /       | /  __  \   /      ||  |/  /     /       |  | ____| ");
            Console.WriteLine(@"   |   (----`|  |  |  | |  ,----'|  '  /     |   (----`  | |__   ");
            Console.WriteLine(@"    \   \    |  |  |  | |  |     |    <       \   \      |___ \  ");
            Console.WriteLine(@".----)   |   |  `--'  | |  `----.|  .  \  .----)   |      ___) | ");
            Console.WriteLine(@"|_______/     \______/   \______||__|\__\ |_______/      |____/  ");
            Console.WriteLine(@"                                                                 ");
            Console.WriteLine(@"");
            Console.ResetColor();
            Console.WriteLine("Welcome to the Socks5 server on reverse project!");
            Console.WriteLine(@"-----------------------------------------------------------------");
            Console.Write("\nSelect the IP: ");
            IPAddress = Console.ReadLine();
            Console.Write("\nSelect the Socks5 port: ");
            Socks5Port = Console.ReadLine();
            Console.Write("\nSelect the WebHost port: ");
            WebHostPort = Console.ReadLine();
            Console.Write("\nSelect Socks5 user credentials if any: ");
            User = Console.ReadLine();
            Console.Write("\nSelect Socks5 password credentials if any: ");
            Password = Console.ReadLine();
            Console.WriteLine("\n-----------------------------------------------------------------");

            Task.Run(() =>
            {
                Socks5 socks5;
                try
                {
                    socks5 = new Socks5(System.Net.IPAddress.Parse(IPAddress), int.Parse(Socks5Port), User, Password);
                    socks5.StartServer(Identifier);
                }
                catch (Exception ex)
                {
                    Socks5Log.WriteErrorLine(ex);
                }
            });

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"\nWebHost started on: http://{IPAddress}:{WebHostPort}\n");
            Console.ResetColor();
            Socks5Log.SetCursorTop();
            BuildHost(IPAddress, WebHostPort).Run();
        }

        private static IHost BuildHost(string IPAddress, string WebHostPort)
        {
            return new HostBuilder()
                .ConfigureWebHost(webconfig =>
                {
                    webconfig.UseKestrel(options =>
                    {
                        options.AddServerHeader = false;
                    })
                    .UseStartup<Startup>()
                    .UseUrls($"http://{IPAddress}:{WebHostPort}");
                })
                .Build();
        }
    }
}
