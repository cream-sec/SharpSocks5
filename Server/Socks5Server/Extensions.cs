using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace System
{
    public static class Extension
    {
        public static long PeekBytes(this Stream stream)
        {
            return stream.Length - stream.Position;
        }

        public static TcpState GetState(this TcpClient tcpClient)
        {
            try
            {
                var foo = IPGlobalProperties.GetIPGlobalProperties()
                  .GetActiveTcpConnections()
                  .SingleOrDefault(x => x.LocalEndPoint.Equals(tcpClient?.Client?.LocalEndPoint)
                                     && x.RemoteEndPoint.Equals(tcpClient?.Client?.RemoteEndPoint)
                  );

                return foo != null ? foo.State : TcpState.Unknown;
            }
            catch
            {
                return TcpState.Unknown;
            }
        }
    }
}
