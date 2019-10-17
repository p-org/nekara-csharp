namespace Nekara.Networking
{
    public class OmniClientConfiguration
    {
        private string host;    // used if Transport == HTTP
        private int port;       // used if Transport == HTTP or TCP

        public OmniClientConfiguration(Transport tMode = Transport.WS)
        {
            this.Transport = tMode;
        }

        public Transport Transport { get; set; }
    }
}
