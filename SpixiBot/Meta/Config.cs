﻿using Fclp;
using IXICore;
using IXICore.Meta;
using IXICore.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SpixiBot.Meta
{
    public class Config
    {
        // Providing pre-defined values
        // Can be read from a file later, or read from the command line
        public static int serverPort = 15235;

        private static int defaultServerPort = 15235;
        private static int defaultTestnetServerPort = 16235;

        public static NetworkType networkType = NetworkType.main;

        public static int apiPort = 8501;
        public static int testnetApiPort = 8601;

        public static Dictionary<string, string> apiUsers = new Dictionary<string, string>();

        public static List<string> apiAllowedIps = new List<string>();
        public static List<string> apiBinds = new List<string>();

        public static string configFilename = "ixian.cfg";
        public static string walletFile = "ixian.wal";

        public static int maxLogSize = 50;
        public static int maxLogCount = 10;

        public static bool disableWebStart = false;

        public static bool onlyShowAddresses = false;

        public static string externalIp = "";

        public static List<byte[]> whiteList = new List<byte[]>();

        // Read-only values
        public static readonly string version = "xsbc-0.5.1-PR1"; // Spixi Bot version

        public static readonly string pushServiceUrl = "https://ipn.ixian.io/v1";

        public static readonly string checkVersionUrl = "https://www.ixian.io/bot-update.txt";
        public static readonly int checkVersionSeconds = 6 * 60 * 60; // 6 hours

        public static readonly int maximumStreamClients = 10000; // Maximum number of stream clients this server can accept

        public static readonly int pushNotificationInterval = 30 * 60 * 1000; // Interval of sending push notifications to clients in milliseconds

        // Quotas
        public static readonly long lastPaidTimeQuota = 10 * 60; // Allow 10 minutes after payment before checking quotas
        public static readonly int infoMessageQuota = 10;  // Allow 10 info messages per 1 data message
        public static readonly int dataMessageQuota = 3; // Allow up to 3 data messages before receiving a transaction signature


        public static string botName = "Spixi Bot"; // This will be moved to the settings pages - upgrade to wallet pages

        public static readonly int maxMessagesPerChannel = 10000; // Maximum messages that will be stored per channel


        // Debugging values
        public static string networkDumpFile = "";

        // Development/testing options
        public static bool generateWalletOnly = false;
        public static string dangerCommandlinePasswordCleartextUnsafe = "";


        // internal
        public static bool changePass = false;

        /// <summary>
        /// Command to execute when a new block is accepted.
        /// </summary>
        public static string blockNotifyCommand = "";

        public static string dataDirectory = "Data";

        private Config()
        {

        }

        private static string outputHelp()
        {
            SpixiBot.Program.noStart = true;

            Console.WriteLine("Starts a new instance of Spixi Bot");
            Console.WriteLine("");
            Console.WriteLine(" SpixiBot.exe [-h] [-v] [-t] [-x] [-c] [-p 10234] [-a 8081] [-i ip] [-w ixian.wal] [-n seed1.ixian.io:10234]");
            Console.WriteLine(" [--config ixian.cfg] [--maxLogSize 50] [--maxLogCount 10] [--disableWebStart] [--netdump]");
            Console.WriteLine(" [--generateWallet] [--walletPassword password_of_wallet]");
            Console.WriteLine("");
            Console.WriteLine("    -h\t\t\t Displays this help");
            Console.WriteLine("    -v\t\t\t Displays version");
            Console.WriteLine("    -t\t\t\t Starts node in testnet mode");
            Console.WriteLine("    -x\t\t\t Change password of an existing wallet");
            Console.WriteLine("    -c\t\t\t Removes cache, peers.dat and ixian.log files before starting");
            Console.WriteLine("    -p\t\t\t Port to listen on");
            Console.WriteLine("    -a\t\t\t HTTP/API port to listen on");
            Console.WriteLine("    -i\t\t\t External IP Address to use");
            Console.WriteLine("    -w\t\t\t Specify location of the ixian.wal file");
            Console.WriteLine("    -n\t\t\t Specify which seed node to use");
            Console.WriteLine("    --config\t\t Specify config filename (default ixian.cfg)");
            Console.WriteLine("    --maxLogSize\t Specify maximum log file size in MB");
            Console.WriteLine("    --maxLogCount\t Specify maximum number of log files");
            Console.WriteLine("    --disableWebStart\t Disable running http://localhost:8081 on startup");
            Console.WriteLine("    --botName\t\t Specify the name of this bot/channel");
            Console.WriteLine("");
            Console.WriteLine("----------- Developer CLI flags -----------");
            Console.WriteLine("    --netdump\t\t Enable netdump for debugging purposes");
            Console.WriteLine("    --generateWallet\t Generates a wallet file and exits, printing the public address. [TESTNET ONLY!]");
            Console.WriteLine("    --walletPassword\t Specify the password for the wallet. [TESTNET ONLY!]");
            Console.WriteLine("");
            Console.WriteLine("----------- Config File Options -----------");
            Console.WriteLine(" Config file options should use parameterName = parameterValue semantics.");
            Console.WriteLine(" Each option should be specified in its own line. Example:");
            Console.WriteLine("    botPort = 10234");
            Console.WriteLine("    apiPort = 8081");
            Console.WriteLine("");
            Console.WriteLine(" Available options:");
            Console.WriteLine("    botPort\t\t Port to listen on (same as -p CLI)");
            Console.WriteLine("    testnetBotPort\t Port to listen on in testnet mode (same as -p CLI)");
            Console.WriteLine("    apiPort\t\t HTTP/API port to listen on (same as -a CLI)");
            Console.WriteLine("    testnetApiPort\t HTTP/API port to listen on in testnet mode (same as -a CLI)");
            Console.WriteLine("    addApiUser\t\t Adds user:password that can access the API (can be used multiple times)");
            Console.WriteLine("    externalIp\t\t External IP Address to use (same as -i CLI)");
            Console.WriteLine("    addPeer\t\t Specify which seed node to use (same as -n CLI) (can be used multiple times)");
            Console.WriteLine("    addTestnetPeer\t Specify which seed node to use in testnet mode (same as -n CLI) (can be used multiple times)");
            Console.WriteLine("    maxLogSize\t\t Specify maximum log file size in MB (same as --maxLogSize CLI)");
            Console.WriteLine("    maxLogCount\t\t Specify maximum number of log files (same as --maxLogCount CLI)");
            Console.WriteLine("    disableWebStart\t 1 to disable running http://localhost:8081 on startup (same as --disableWebStart CLI)");
            Console.WriteLine("    walletNotify\t Execute command when a wallet transaction changes");
            Console.WriteLine("    blockNotify\t Execute command when the block changes");

            return "";
        }

        private static string outputVersion()
        {
            SpixiBot.Program.noStart = true;

            // Do nothing since version is the first thing displayed

            return "";
        }


        private static void readConfigFile(string filename)
        {
            if (!File.Exists(filename))
            {
                return;
            }
            Logging.info("Reading config file: " + filename);
            List<string> lines = File.ReadAllLines(filename).ToList();
            foreach (string line in lines)
            {
                string[] option = line.Split('=');
                if (option.Length < 2)
                {
                    continue;
                }
                string key = option[0].Trim(new char[] { ' ', '\t', '\r', '\n' });
                string value = option[1].Trim(new char[] { ' ', '\t', '\r', '\n' });

                if (key.StartsWith(";"))
                {
                    continue;
                }
                Logging.info("Processing config parameter '" + key + "' = '" + value + "'");
                switch (key)
                {
                    case "botPort":
                        Config.defaultServerPort = int.Parse(value);
                        break;
                    case "testnetBotPort":
                        Config.defaultTestnetServerPort = int.Parse(value);
                        break;
                    case "apiPort":
                        apiPort = int.Parse(value);
                        break;
                    case "testnetApiPort":
                        testnetApiPort = int.Parse(value);
                        break;
                    case "addApiUser":
                        string[] credential = value.Split(':');
                        if (credential.Length == 2)
                        {
                            apiUsers.Add(credential[0], credential[1]);
                        }
                        break;
                    case "externalIp":
                        externalIp = value;
                        break;
                    case "addPeer":
                        CoreNetworkUtils.seedNodes.Add(new string[2] { value, null });
                        break;
                    case "addTestnetPeer":
                        CoreNetworkUtils.seedTestNetNodes.Add(new string[2] { value, null });
                        break;
                    case "maxLogSize":
                        maxLogSize = int.Parse(value);
                        break;
                    case "maxLogCount":
                        maxLogCount = int.Parse(value);
                        break;
                    case "disableWebStart":
                        if (int.Parse(value) != 0)
                        {
                            disableWebStart = true;
                        }
                        break;
                    case "walletNotify":
                        CoreConfig.walletNotifyCommand = value;
                        break;
                    case "blockNotify":
                        Config.blockNotifyCommand = value;
                        break;
                    default:
                        // unknown key
                        Logging.warn("Unknown config parameter was specified '" + key + "'");
                        break;
                }
            }
        }
        public static void readFromCommandLine(string[] args)
        {
            // first pass
            var cmd_parser = new FluentCommandLineParser();

            // help
            cmd_parser.SetupHelp("h", "help").Callback(text => outputHelp());

            // config file
            cmd_parser.Setup<string>("config").Callback(value => configFilename = value).Required();

            cmd_parser.Parse(args);

            if (SpixiBot.Program.noStart)
            {
                return;
            }

            readConfigFile(configFilename);



            // second pass
            cmd_parser = new FluentCommandLineParser();

            // testnet
            cmd_parser.Setup<bool>('t', "testnet").Callback(value => networkType = NetworkType.test).Required();

            cmd_parser.Parse(args);

            if (networkType == NetworkType.test)
            {
                serverPort = defaultTestnetServerPort;
                apiPort = testnetApiPort;
            }
            else
            {
                serverPort = defaultServerPort;
            }

            string seedNode = "";

            // third pass
            cmd_parser = new FluentCommandLineParser();

            bool start_clean = false; // Flag to determine if node should delete cache+logs

            // version
            cmd_parser.Setup<bool>('v', "version").Callback(text => outputVersion());

            // Check for password change
            cmd_parser.Setup<bool>('x', "changepass").Callback(value => changePass = value).Required();

            // Check for clean parameter
            cmd_parser.Setup<bool>('c', "clean").Callback(value => start_clean = value).Required();


            cmd_parser.Setup<int>('p', "port").Callback(value => Config.serverPort = value).Required();

            cmd_parser.Setup<int>('a', "apiport").Callback(value => apiPort = value).Required();

            cmd_parser.Setup<string>('i', "ip").Callback(value => externalIp = value).Required();

            cmd_parser.Setup<string>('w', "wallet").Callback(value => walletFile = value).Required();

            cmd_parser.Setup<string>('n', "node").Callback(value => seedNode = value).Required();

            cmd_parser.Setup<int>("maxLogSize").Callback(value => maxLogSize = value).Required();

            cmd_parser.Setup<int>("maxLogCount").Callback(value => maxLogCount = value).Required();

            cmd_parser.Setup<bool>("disableWebStart").Callback(value => disableWebStart = true).Required();

            cmd_parser.Setup<bool>("onlyShowAddresses").Callback(value => onlyShowAddresses = true).Required();

            cmd_parser.Setup<string>("botName").Callback(value => botName = value).Required();


            // Debug
            cmd_parser.Setup<string>("netdump").Callback(value => networkDumpFile = value).SetDefault("");

            cmd_parser.Setup<bool>("generateWallet").Callback(value => generateWalletOnly = value).SetDefault(false);

            cmd_parser.Setup<string>("walletPassword").Callback(value => dangerCommandlinePasswordCleartextUnsafe = value).SetDefault("");

            cmd_parser.Parse(args);


            // Validate parameters

            if (start_clean)
            {
                Node.cleanCacheAndLogs();
            }

            if (seedNode != "")
            {
                if (networkType == NetworkType.test)
                {
                    CoreNetworkUtils.seedTestNetNodes = new List<string[]>
                        {
                            new string[2] { seedNode, null }
                        };
                }
                else
                {
                    CoreNetworkUtils.seedNodes = new List<string[]>
                        {
                            new string[2] { seedNode, null }
                        };
                }
            }
        }

    }

}