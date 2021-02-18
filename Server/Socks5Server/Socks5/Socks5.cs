using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Socks5Server
{
    public class Socks5
    {
        TcpListener TcpListener { get; set; }
        IPEndPoint LocalEndPoint { get; set; }
        public bool AuthRequired { get; set; } = false;
        public string Username { get; set; }
        public string Password { get; set; }

        public Socks5(IPAddress address, int port, string username = "", string password = "")
        {
            try
            {
                this.TcpListener = new TcpListener(address, port);
                this.LocalEndPoint = new IPEndPoint(address, port);

                if (!string.IsNullOrEmpty(username))
                {
                    this.AuthRequired = true;
                    this.Username = username;
                    this.Password = password;
                }
            }
            catch (Exception ex)
            {
                Socks5Log.WriteErrorLine(ex);
            }
        }

        public class Command
        {
            public class Constants
            {
                public enum Command : byte
                {
                    Connect = 0x01,
                    Bind = 0x02,
                    UdpAssociate = 0x03
                }
                public enum AddressType : byte
                {
                    IPv4 = 0x01,
                    Domain = 0x03,
                    IPv6 = 0x04
                }
                public enum AuthMethods : byte
                {
                    NoAuthenticationRequired = 0x00,
                    GSSAPI = 0x01,
                    UsernamePassword = 0x02,
                    NonAcceptableMethod = 0xFF
                }
            }

            public byte SocksVersion { get; private set; }
            public Constants.Command SocksCommand { get; private set; }
            public Constants.AddressType AddressType { get; private set; }
            public string Domain { get; private set; }
            public IPAddress DestinationAddress { get; private set; }
            public ushort DestinationPort { get; private set; }
            public bool DnsSuccess { get; private set; }

            public bool Parse(byte[] response)
            {
                using (MemoryStream stream = new MemoryStream(response))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {

                        //        +----+-----+-------+------+----------+----------+
                        //        |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
                        //        +----+-----+-------+------+----------+----------+
                        //        | 1  |  1  | X'00' |  1   | Variable |    2     |
                        //        +----+-----+-------+------+----------+----------+
                        //
                        //     Where:
                        //
                        //          o  VER    protocol version: X'05'
                        //          o  CMD
                        //             o  CONNECT X'01'
                        //             o  BIND X'02'
                        //             o  UDP ASSOCIATE X'03'
                        //          o  RSV    RESERVED
                        //          o  ATYP   address type of following address
                        //             o  IP V4 address: X'01'
                        //             o  DOMAINNAME: X'03'
                        //             o  IP V6 address: X'04'
                        //          o  DST.ADDR       desired destination address
                        //          o  DST.PORT desired destination port in network octet
                        //             order

                        if (reader.BaseStream.Length < 10)
                            return false;

                        this.SocksVersion = reader.ReadByte();

                        this.SocksCommand = (Constants.Command)reader.ReadByte();
                        if (!Enum.IsDefined(typeof(Constants.Command), this.SocksCommand))
                            return false;

                        if (reader.ReadByte() != 0)
                            return false;

                        this.AddressType = (Constants.AddressType)reader.ReadByte();
                        if (!Enum.IsDefined(typeof(Constants.AddressType), this.AddressType))
                            return false;

                        if (this.AddressType == Constants.AddressType.IPv4)
                        {
                            if (reader.BaseStream.PeekBytes() != 6)
                                return false;
                            this.DestinationAddress = new IPAddress((long)(uint)reader.ReadInt32());
                        }
                        else if (this.AddressType == Constants.AddressType.IPv6)
                        {
                            if (reader.BaseStream.PeekBytes() != 18)
                                return false;
                            this.DestinationAddress = new IPAddress(reader.ReadBytes(16));
                        }
                        else if (this.AddressType == Constants.AddressType.Domain)
                        {
                            byte domainLength = reader.ReadByte();
                            if (reader.BaseStream.PeekBytes() != domainLength + 2)
                                return false;
                            this.Domain = Encoding.UTF8.GetString(reader.ReadBytes(domainLength));

                            try
                            {
                                IPHostEntry dnsResults = Dns.GetHostEntry(this.Domain);
                                this.DestinationAddress = dnsResults.AddressList.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault();
                                this.DnsSuccess = true;
                            }
                            catch (Exception)
                            { }

                            if (this.DestinationAddress == null)
                            {
                                this.DestinationAddress = IPAddress.Loopback;
                                this.DnsSuccess = false;
                            }
                        }

                        this.DestinationPort = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());
                        return true;
                    }
                }
            }
        }

        public static class ConnectionManager
        {
            public static ConcurrentDictionary<string, ConnectionFactory> Keys { get; set; } = new ConcurrentDictionary<string, ConnectionFactory>();

            public static void CreateConnection(string identifier, TcpClient _client, IPEndPoint _localEndPoint, string username, string password)
            {
                Task.Run(() =>
                {
                    try
                    {
                        Guid guid = Guid.NewGuid();
                        Keys.TryAdd(guid.ToString(), new ConnectionFactory(identifier, _client, _localEndPoint, guid, username, password));
                        Keys.FirstOrDefault(_ => _.Key == guid.ToString()).Value?.ConnectionWorker();
                    }
                    catch (Exception ex)
                    {
                        Socks5Log.WriteErrorLine(ex);
                    }
                });
            }

            public static void UpdateConnection(string identifier, Socks5State Socks5State)
            {
                if (Socks5State.Socks5Status == Socks5Status.NewConnection)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await Keys.FirstOrDefault(_ => _.Key == Socks5State.Guid.ToString()).Value?.ConnectionReady(Socks5State);
                        }
                        catch (Exception ex)
                        {
                            Socks5Log.WriteErrorLine(ex);
                        }
                        finally
                        {
                            try
                            {
                                QueueManager.EnqueueElement(identifier, new Socks5State
                                {
                                    Guid = Socks5State.Guid,
                                    Socks5Status = Socks5Status.Error,
                                });

                                Keys.FirstOrDefault(_ => _.Key == Socks5State.Guid.ToString()).Value?.Dispose();
                                Keys.TryRemove(Socks5State.Guid.ToString(), out _);
                            }
                            catch (Exception ex)
                            {
                                Socks5Log.WriteErrorLine(ex);
                            }
                        }
                    });
                }
                if (Socks5State.Socks5Status == Socks5Status.Ok)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            Keys.FirstOrDefault(_ => _.Key == Socks5State.Guid.ToString()).Value?.Socks5Data?.OnDataReceived(Socks5State);
                        }
                        catch { }
                    });
                }
                if (Socks5State.Socks5Status == Socks5Status.Error)
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            Keys.FirstOrDefault(_ => _.Key == Socks5State.Guid.ToString()).Value?.Dispose();
                            Keys.TryRemove(Socks5State.Guid.ToString(), out _);
                        }
                        catch { }
                    });
                }

                Socks5Log.WriteKeysInfo($"Currently active sockets: {Keys.Count}");
            }
        }

        public class ConnectionFactory : IDisposable
        {
            public IPEndPoint LocalEndPoint { get; set; }
            public TcpClient Client { get; set; }
            public Guid Guid { get; set; }
            public Socks5Data Socks5Data { get; set; }
            public string Identifier { get; private set; }
            public bool AuthRequired { get; set; } = false;
            public string Username { get; set; }
            public string Password { get; set; }

            public ConnectionFactory(string identifier, TcpClient client, IPEndPoint localEndPoint, Guid guid)
            {
                this.Identifier = identifier;
                this.LocalEndPoint = localEndPoint;
                this.Client = client;
                this.Guid = guid;
            }

            public ConnectionFactory(string identifier, TcpClient client, IPEndPoint localEndPoint, Guid guid, string username, string password)
            {
                this.Identifier = identifier;
                this.LocalEndPoint = localEndPoint;
                this.Client = client;
                this.Guid = guid;
                this.Username = username;
                this.Password = password;

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    this.AuthRequired = true;
                }

                TcpClientMonitor();
            }

            public void TcpClientMonitor()
            {
                Task.Run(() =>
                {
                    while (this.Client?.GetState() == TcpState.Established) { Thread.Sleep(100); }

                    ConnectionManager.UpdateConnection(Identifier, new Socks5State
                    {
                        Socks5Status = Socks5Status.Error,
                        Guid = this.Guid
                    });

                    Socks5Log.WriteKeysInfo($"Currently active sockets: {ConnectionManager.Keys.Count}");
                });
            }

            public void ConnectionWorker()
            {
                var stream = Client.GetStream();

                var firstRequest = StreamReadToEnd(stream);

                var method = GetConnectionMethod(firstRequest);

                if (method == Socks5.Command.Constants.AuthMethods.NoAuthenticationRequired ||
                    method == Socks5.Command.Constants.AuthMethods.GSSAPI)
                {
                    StreamWrite(stream, ReplyAuthentication(Command.Constants.AuthMethods.NoAuthenticationRequired));

                    var secondRecive = StreamReadToEnd(stream);

                    QueueManager.EnqueueElement(this.Identifier, new Socks5State
                    {
                        Guid = this.Guid,
                        Bytes = secondRecive,
                        Socks5Status = Socks5Status.NewConnection
                    });
                }
                else if (method == Command.Constants.AuthMethods.UsernamePassword)
                {
                    if (AuthRequired)
                    {
                        StreamWrite(stream, ReplyAuthentication(Command.Constants.AuthMethods.UsernamePassword));

                        var secondRecive = StreamReadToEnd(stream);
                        var usernameLength = secondRecive[1];
                        var username = Encoding.ASCII.GetString(secondRecive, 2, usernameLength);
                        var passwordLength = secondRecive[1 + usernameLength + 1];
                        var password = Encoding.ASCII.GetString(secondRecive, 3 + usernameLength, passwordLength);

                        if (username == this.Username && password == this.Password)
                        {
                            ReplyAuthentication(stream, 0);
                        }
                        else
                        {
                            ReplyAuthentication(stream, 255);
                        }

                        var thirdRecive = StreamReadToEnd(stream);

                        QueueManager.EnqueueElement(this.Identifier, new Socks5State
                        {
                            Guid = this.Guid,
                            Bytes = thirdRecive,
                            Socks5Status = Socks5Status.NewConnection
                        });
                    }
                    else
                    {
                        StreamWrite(stream, ReplyAuthentication(Command.Constants.AuthMethods.NonAcceptableMethod));
                    }
                }
                else
                {
                    StreamWrite(stream, ReplyAuthentication(Command.Constants.AuthMethods.NonAcceptableMethod));
                }

                Socks5Log.WriteConnectionInfo($"New connection created: {Guid}");
            }

            public async Task ConnectionReady(Socks5State Socks5State)
            {
                if (Socks5State.ProtocolType == ProtocolType.Tcp)
                {
                    StreamWrite(Client.GetStream(), ConfirmationResponse(LocalEndPoint.Port));

                    using (Socks5Data = new Socks5TCPData(this.Identifier, this.Guid, this.Client))
                    {
                        await Socks5Data.Start();
                    }

                }

                if (Socks5State.ProtocolType == ProtocolType.Udp)
                {
                    UdpClient udpClient = new UdpClient(new IPEndPoint(this.LocalEndPoint.Address, 0));

                    StreamWrite(this.Client.GetStream(), ConfirmationResponse((udpClient.Client.LocalEndPoint as IPEndPoint).Port));

                    using (Socks5Data = new Socks5UDPData(this.Identifier, this.Guid, this.Client, udpClient))
                    {
                        await Socks5Data.Start();
                    }
                }
            }

            private void ReplyAuthentication(NetworkStream stream, Byte status)
            {
                stream.Write(new byte[] { 1, (Byte)status }, 0, 2);
            }

            private byte[] StreamReadToEnd(NetworkStream stream)
            {
                byte[] bytes = new byte[1024];
                int count = stream.Read(bytes, 0, bytes.Length);
                byte[] result = new byte[count];
                Array.Copy(bytes, result, count);
                return result;
            }

            private void StreamWrite(NetworkStream stream, byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length);
            }

            private Command.Constants.AuthMethods GetConnectionMethod(byte[] firstRequest)
            {
                try
                {
                    if (firstRequest[0] == 5 && firstRequest[1] > 0)
                    {
                        return (Command.Constants.AuthMethods)firstRequest[1];
                    }
                }
                catch
                {
                    return Command.Constants.AuthMethods.NonAcceptableMethod;
                }
                return Command.Constants.AuthMethods.NonAcceptableMethod;
            }

            private byte[] ReplyAuthentication(Command.Constants.AuthMethods authMethod)
            {
                byte[] responce = new byte[] { 0x05, (byte)authMethod };
                return responce;
            }

            private byte[] ConfirmationResponse(int socketPort)
            {
                byte[] port = BitConverter.GetBytes(socketPort);
                IPAddress address = this.LocalEndPoint.Address;
                byte[] ip = address.GetAddressBytes();
                byte[] response = new byte[]
                { 5, 0, 0, 1, ip[0], ip[1], ip[2], ip[3], port[1], port[0] };
                return response;
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
                    this.Client?.Close();
                    this.LocalEndPoint = null;
                    this.Client = null;
                }
            }
        }

        public void StartServer(string identifier)
        {
            try
            {
                TcpListener.Start();
            }
            catch (Exception ex)
            {
                Socks5Log.WriteErrorLine(ex);
            }

            while (true)
            {
                try
                {
                    TcpClient client = TcpListener.AcceptTcpClient();

                    ConnectionManager.CreateConnection(identifier, client, LocalEndPoint, Username, Password);

                }
                catch (Exception ex)
                {
                    Socks5Log.WriteErrorLine(ex);
                }
            }
        }

        public abstract class Socks5Data : IDisposable
        {
            public Socks5Data(string identifier)
            {
                this.identifier = identifier;
            }

            public abstract void OnDataReceived(Socks5State Socks5State);
            public abstract Task Start();
            public string identifier { get; private set; }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected abstract void Dispose(bool disposing);
        }

        public class Socks5TCPData : Socks5Data
        {
            public class StateObject
            {
                public Socket workSocket = null;
                public const int BufferSize = 4096;
                public byte[] buffer = new byte[BufferSize];
            }

            private TcpClient _client;
            private Guid _guid;

            public Socks5TCPData(string identifier, Guid guid, TcpClient client) : base(identifier)
            {
                _guid = guid;
                _client = client;
            }

            public override void OnDataReceived(Socks5State Socks5State)
            {
                try
                {
                    _client.Client.Send(Socks5State.Bytes);
                }
                catch { }
            }

            public async Task ClientRead()
            {
                var buffer = new byte[4096];
                var ns = _client.GetStream();
                while (true)
                {
                    var bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) return;

                    byte[] b = new byte[bytesRead];
                    Array.Copy(buffer, 0, b, 0, bytesRead);
                    QueueManager.EnqueueElement(this.identifier, new Socks5State
                    {
                        Guid = _guid,
                        Bytes = b,
                        Socks5Status = Socks5Status.Ok,
                    });
                }
            }

            public override async Task Start()
            {
                try
                {
                    await ClientRead();
                }
                catch { }
            }

            bool disposed = false;

            protected override void Dispose(bool disposing)
            {

                if (disposed)
                    return;

                disposed = true;

                if (disposing)
                {
                    _client?.Close();
                    _client = null;
                }
            }
        }

        public class Socks5UDPData : Socks5Data
        {

            private TcpClient _client;
            private Guid _guid;
            private UdpClient _udpClient;
            private IPEndPoint _iPEndPoint;

            public Socks5UDPData(string identifier, Guid guid, TcpClient client, UdpClient udpClient) : base(identifier)
            {
                _guid = guid;
                _client = client;
                _udpClient = udpClient;
            }

            public override void OnDataReceived(Socks5State Socks5State)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await _udpClient.SendAsync(Socks5State.Bytes, Socks5State.Bytes.Count(), _iPEndPoint);
                    }
                    catch { }
                });
            }

            public async Task ClientRead()
            {
                var buffer = new byte[4096];
                var ns = _client.GetStream();
                while (true)
                {
                    var bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) return;
                }
            }

            private void OnUdpDataReceive(IAsyncResult result)
            {
                try
                {
                    var state = (UdpClient)result.AsyncState;
                    byte[] receivedBytes = state.EndReceive(result, ref _iPEndPoint);

                    if (receivedBytes.Count() > 0)
                    {
                        QueueManager.EnqueueElement(this.identifier, new Socks5State
                        {
                            Guid = _guid,
                            Bytes = receivedBytes,
                            Socks5Status = Socks5Status.Ok,
                        });
                    }

                    _udpClient.BeginReceive(OnUdpDataReceive, _udpClient);
                }
                catch { }
            }

            public override async Task Start()
            {
                try
                {
                    _udpClient.BeginReceive(OnUdpDataReceive, _udpClient);
                    await ClientRead();
                }
                catch { }
            }

            bool disposed = false;
            protected override void Dispose(bool disposing)
            {

                if (disposed)
                    return;

                disposed = true;

                if (disposing)
                {
                    _client?.Close();
                    _client = null;
                    _udpClient?.Client?.Close();
                    _udpClient = null;
                }
            }
        }
    }
}
