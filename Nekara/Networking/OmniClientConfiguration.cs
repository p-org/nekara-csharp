namespace Nekara.Networking
{
    public class OmniClientConfiguration
    {
        private Transport _transport;
        private string host;    // used if Transport == HTTP
        private int port;       // used if Transport == HTTP or TCP

        public OmniClientConfiguration()
        {
            this._transport = Transport.WS;
        }

        public Transport transport
        {
            get { return this._transport; }
            set { this._transport = value; }
        }
    }
}
