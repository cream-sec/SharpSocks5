using System;
using System.IO;
using System.Net;

namespace Socks5Client
{
    public static class HttpClient
    {
        private static bool _enabled = true;
        private static string _url = string.Empty;

        public static void StartReceive(string ip, string port)
        {
            _url = $"http://{ip}:{port}/socks.html";
            _enabled = true;

            while (_enabled)
            {
                try
                {
                    HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(_url);
                    request.Proxy = null;
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            using (Stream stream = response.GetResponseStream())
                            {
                                using (StreamReader reader = new StreamReader(stream))
                                {
                                    string result = reader.ReadToEnd();
                                    if (!string.IsNullOrEmpty(result))
                                    {
                                        Serializer serializer = new Serializer();
                                        var Socks5State = serializer.Deserialize(result);
                                        ConnectionManager.UpdateConnection(Socks5State);
                                    }
                                }
                            }
                        }
                    }
                    request = null;
                }
                catch (Exception ex)
                {
                    Socks5Log.WriteConnectionInfo($"Http client error on reaching the endpoint");
                    Socks5Log.WriteErrorLine(ex);
                }
            }
        }

        public static void StopReceive()
        {
            _enabled = false;
        }

        public static void Send(Socks5State Socks5State)
        {
            try
            {
                using (var client = new WebClient())
                {
                    Serializer serializer = new Serializer();
                    client.UseDefaultCredentials = true;
                    client.Proxy = WebRequest.DefaultWebProxy;
                    client.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                    client.UploadString(_url, serializer.Serialize(Socks5State));
                }
            }
            catch (Exception ex)
            {
                Socks5Log.WriteConnectionInfo($"Http client error on reaching the endpoint");
                Socks5Log.WriteErrorLine(ex);
            }
        }
    }
}
