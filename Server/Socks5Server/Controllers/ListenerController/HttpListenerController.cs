using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using static Socks5Server.Socks5;
using System;
using Socks5Server;

namespace Covenant.Controllers
{
    [AllowAnonymous]
    public class HttpListenerController : Controller
    {

        public HttpListenerController()
        {

        }


        [DisableRequestSizeLimit]
        [HttpGet, AllowAnonymous]
        public async Task<ActionResult<string>> Get()
        {
            try
            {
                Socks5State dequeueElement = QueueManager.DequeueElement("socks");
                string response = string.Empty;
                if (dequeueElement != null)
                {
                    Serializer serializer = new Serializer();
                    response = serializer.Serialize(dequeueElement);
                }
                return Ok(response);
            }
            catch(Exception ex)
            {
                return Ok(null);
            }
        }

        [DisableRequestSizeLimit]
        [HttpPost, AllowAnonymous]
        public async Task<ActionResult<string>> Post()
        {
            try
            {
                using (StreamReader reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8))
                {
                    string body = await reader.ReadToEndAsync();
                    Serializer serializer = new Serializer();
                    var result = serializer.Deserialize(body);
                    ConnectionManager.UpdateConnection("socks", result);
                    string response = string.Empty;
                    return Ok(response);
                }
            }
            catch
            {
                return Ok(null);
            }
        }

    }
}
