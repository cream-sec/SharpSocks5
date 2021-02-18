using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Socks5Client
{
    public class Socks5UDPData : Socks5Data
    {

        private UdpClient _udpClient;
        private Guid _guid;
        private IPEndPoint _iPEndPoint;
        private UdpAssociate _udpAssociate = new UdpAssociate();

        public Socks5UDPData(Guid guid, UdpClient udpClient)
        {
            _udpClient = udpClient;
            _guid = guid;
        }

        private void OnDataReceive(IAsyncResult result)
        {
            try
            {
                var state = (UdpClient)result.AsyncState;
                byte[] receivedBytes = state.EndReceive(result, ref _iPEndPoint);

                if (receivedBytes.Count() > 0)
                {
                    var packet = _udpAssociate.PackUdp(_udpAssociate.Domain, _iPEndPoint.Address, (ushort)_iPEndPoint.Port, receivedBytes, receivedBytes.Count());

                    HttpClient.Send(new Socks5State
                    {
                        Guid = _guid,
                        Bytes = packet,
                        Socks5Status = Socks5Status.Ok,
                    });
                }

                _udpClient.BeginReceive(OnDataReceive, _udpClient);
            }
            catch { }
        }

        public override void OnDataReceived(byte[] bytes)
        {
            try
            {
                _udpAssociate = new UdpAssociate();
                _udpAssociate.Parse(bytes);

                _udpClient.BeginSend(_udpAssociate.Data,
                                     _udpAssociate.Data.Count(),
                                     new IPEndPoint(_udpAssociate.DestinationAddress, _udpAssociate.DestinationPort),
                                     new AsyncCallback(DataSent),
                                     _udpClient);
            }
            catch { }
        }

        private void DataSent(IAsyncResult res)
        {
            try
            {
                int sent = ((UdpClient)res.AsyncState).EndSend(res);
                if (sent < 0)
                {
                    _udpClient?.Client?.Close();
                    return;
                }
            }
            catch { }
        }

        public override void Start()
        {
            try
            {
                _udpClient.BeginReceive(OnDataReceive, _udpClient);
            }
            catch (Exception ex)
            {
                Socks5Log.WriteErrorLine(ex);
            }
        }

        bool disposed = false;
        protected override void Dispose(bool disposing)
        {

            if (disposed)
                return;

            disposed = true;

            if (disposing)
            {
                _udpClient?.Client?.Close();
                _udpClient = null;
            }
        }
    }
}
