using System;
using System.Net.Sockets;

namespace Socks5Client
{
    public class Socks5State
    {
        public byte[] Bytes { get; set; }
        public Guid Guid { get; set; }
        public ProtocolType ProtocolType { get; set; } = ProtocolType.Unknown;
        public Socks5Status Socks5Status { get; set; }
    }
}
