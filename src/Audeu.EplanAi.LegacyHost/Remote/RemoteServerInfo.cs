namespace Audeu.EplanAi.LegacyHost.Remote
{
    public sealed class RemoteServerInfo
    {
        public string ServerName { get; set; }
        public int ServerPort { get; set; }
        public int EplanProcessId { get; set; }
        public string EplanVariant { get; set; }
        public string EplanVersion { get; set; }
        public bool Is64Bit { get; set; }
    }
}
