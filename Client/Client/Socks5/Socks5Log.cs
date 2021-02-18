using System;
using System.Threading;

namespace Socks5Client
{
    public static class Socks5Log
    {
        private static int CursorPosition = 0;
        private static object locker = new object();

        public static void SetCursorTop()
        {
            CursorPosition = Console.CursorTop;
        }
        public static void WriteErrorLine(Exception ex)
        {
            //
        }

        public static void WriteConnectionInfo(string text)
        {
            new Thread(() =>
            {
                lock (locker)
                {
                    Console.SetCursorPosition(0, CursorPosition);
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    Console.Write($"{DateTime.Now}: {text}     ");
                    Console.ResetColor();
                    Console.SetCursorPosition(0, CursorPosition);
                }
            }).Start();
        }

        public static void WriteKeysInfo(string text)
        {
            new Thread(() =>
            {
                lock (locker)
                {
                    Console.SetCursorPosition(0, CursorPosition + 1);
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write($"{DateTime.Now}: {text}     ");
                    Console.ResetColor();
                    Console.SetCursorPosition(0, CursorPosition + 1);
                }
            }).Start();
        }
    }
}

