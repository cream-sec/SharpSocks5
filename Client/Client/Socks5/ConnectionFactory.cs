using System;
using System.Net;
using System.Net.Sockets;

namespace Socks5Client
{
    public class ConnectionFactory : IDisposable
    {
        public Socks5Command Socks5Command { get; private set; }
        public Guid Guid { get; private set; }
        public Socks5Data Socks5Data { get; set; }

        public ConnectionFactory(Socks5Command socks5Command, Guid guid)
        {
            this.Socks5Command = socks5Command;
            this.Guid = guid;
            CreateConnection();
        }

        public void CreateConnection()
        {
            if (Socks5Command.SocksCommand == Socks5Command.Constants.Command.Connect)
            {
                Socket socket = new Socket(Socks5Command.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.Connect(new IPEndPoint(Socks5Command.DestinationAddress, Socks5Command.DestinationPort));
                this.Socks5Data = new Socks5TCPData(this.Guid, socket);
            }

            if (Socks5Command.SocksCommand == Socks5Command.Constants.Command.UdpAssociate)
            {
                UdpClient udpClient = new UdpClient(new IPEndPoint(0, 0));
                this.Socks5Data = new Socks5UDPData(this.Guid, udpClient);
            }

            if (this.Socks5Data != null)
            {
                this.Socks5Data.Start();

                HttpClient.Send(new Socks5State
                {
                    Guid = this.Guid,
                    Socks5Status = Socks5Status.NewConnection,
                    ProtocolType = Socks5Command.ProtocolType
                });
            }
        }

        bool disposed = false;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {

            if (disposed)
                return;

            disposed = true;

            if (disposing)
            {
                this.Socks5Command = null;
                this.Socks5Data?.Dispose();
                this.Socks5Data = null;
            }
        }
    }
}
