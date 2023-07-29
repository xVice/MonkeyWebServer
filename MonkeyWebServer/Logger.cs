using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MonkeyWebServer
{
    public static class Logger
    {
        private static object lockObj = new object();

        public static void LogError(string message, bool verbose = true)
        {
            if (!verbose)
            {
                return;
            }
            FormatMessage("§6SERVER §10!!§4", message);

        }

        public static void LogIncoming(string message, bool verbose = true)
        {
            if (!verbose)
            {
                return;
            }
            FormatMessage("§6SERVER §10<-§8", message);

        }

        public static void Log(string message, bool verbose = true)
        {
            if (!verbose)
            {
                return;
            }
            FormatMessage("§6SERVER §10--§8", message);

        }

        public static void LogOutgoing(string message, bool verbose = true)
        {
            if (!verbose)
            {
                return;
            }
            FormatMessage("§6SERVER §10->§8", message);

        }

        private static void FormatMessage(string prefix, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMessage = $"[{timestamp}] {prefix} {message}";
            ParseMessage(formattedMessage);
        }

        private static void ParseMessage(string message)
        {
            string pattern = @"§(\d+)";
            Regex regex = new Regex(pattern);
            int defaultColor = 7; // default console color (white)

            string[] segments = regex.Split(message);

            for (int i = 0; i < segments.Length; i++)
            {
                if (i % 2 == 1) // segments at odd indices are color codes
                {
                    int colorCode = int.Parse(segments[i]);
                    ConsoleColor consoleColor = (ConsoleColor)colorCode;
                    Console.ForegroundColor = consoleColor;
                }
                else
                {
                    Console.Write(segments[i]);
                }
            }

            Console.Write(Environment.NewLine);
            Console.ResetColor(); // reset the console color after parsing
        }


    }
}