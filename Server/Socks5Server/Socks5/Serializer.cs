using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace Socks5Server
{
    public class Serializer
    {
        public Serializer()
        {

        }

        public string Serialize(Socks5State data)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Socks5State));
            using (StringWriter stringWriter = new StringWriter())
            {

                xmlSerializer.Serialize(stringWriter, data);
                return stringWriter.ToString();
            }
        }

        public Socks5State Deserialize(string data)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Socks5State));
            using (StringReader stringReader = new StringReader(data))
            {
                return xmlSerializer.Deserialize(stringReader) as Socks5State;
            }
        }
    }
}
