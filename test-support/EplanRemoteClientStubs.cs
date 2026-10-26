using System;
using System.Collections.Generic;

namespace Eplan.EplApi.RemoteClient
{
    public sealed class EplanServerData
    {
        public EplanServerData(int eplanProcessId)
        {
            EplanProcessID = eplanProcessId;
        }

        public string EplanPath { get; set; }
        public int EplanProcessID { get; }
        public string EplanVariant { get; set; }
        public string EplanVersion { get; set; }
        public string ErrorMsg { get; set; }
        public bool Is64Bit { get; set; }
        public string ServerName { get; set; }
        public int ServerPort { get; set; }
    }

    public sealed class EplanRemoteClient : IDisposable
    {
        public static List<EplanServerData> ActiveServers { get; } = new List<EplanServerData>();
        public static string LastConnectComputer { get; private set; }
        public static string LastConnectPort { get; private set; }
        public static TimeSpan LastConnectTimeout { get; private set; }
        public static string LastAction { get; private set; }
        public static bool PingResult { get; set; } = true;
        public static bool ExecuteActionResult { get; set; } = true;
        public static bool DisconnectCalled { get; private set; }

        public bool SynchronousMode { get; set; }

        public static void ResetTestState()
        {
            ActiveServers.Clear();
            LastConnectComputer = null;
            LastConnectPort = null;
            LastConnectTimeout = default(TimeSpan);
            LastAction = null;
            PingResult = true;
            ExecuteActionResult = true;
            DisconnectCalled = false;
        }

        public void GetActiveEplanServersOnLocalMachine(out List<EplanServerData> servers)
        {
            servers = new List<EplanServerData>(ActiveServers);
        }

        public void Connect(string computerName, string port, TimeSpan interval)
        {
            LastConnectComputer = computerName;
            LastConnectPort = port;
            LastConnectTimeout = interval;
        }

        public bool Ping()
        {
            return PingResult;
        }

        public bool ExecuteAction(string fullAction)
        {
            LastAction = fullAction;
            return ExecuteActionResult;
        }

        public void Disconnect()
        {
            DisconnectCalled = true;
        }

        public void Dispose()
        {
        }
    }
}
