using System;

namespace Socks5Client
{
    public class Program
    {
        static void Main(string[] args)
        {
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
            Console.WriteLine("Welcome to the Socks5 client on reverse project!");
            Console.WriteLine(@"-----------------------------------------------------------------");
            Console.Write("\nSelect the WebHost IP: ");
            string IPAddress = Console.ReadLine();
            Console.Write("\nSelect the WebHost port: ");
            string Port = Console.ReadLine();
            Console.WriteLine("\n-----------------------------------------------------------------\n");

            Socks5Log.SetCursorTop();
            HttpClient.StartReceive(IPAddress, Port);
        }
    }
}
