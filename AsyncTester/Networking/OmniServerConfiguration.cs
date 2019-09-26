using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncTester.Core
{
    // AsyncTester could be used in a network setting. Easily switch between transports with this config flag.
    //   IPC - Inter-process communication (.net native)
    //   HTTP - HyperText Transport Protocol (HTTP/1)
    //   GRPC - gRPC Remote Procedure Calls (implemented over HTTP/2)
    //   WS - WebSocket (implemented over HTTP - this has a different communication pattern as the server-side can "push" to the client)
    //   TCP - Raw TCP (Transmission Control Protocol)
    public enum Transport { IPC, HTTP, GRPC, WS, TCP }


    public class OmniServerConfiguration
    {
        private Transport _transport;
        private string host;    // used if Transport == HTTP
        private int port;       // used if Transport == HTTP or TCP

        public OmniServerConfiguration()
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
