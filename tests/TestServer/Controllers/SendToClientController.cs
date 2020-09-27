using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using SignalARRR;
using SignalARRR.Server;
using SignalARRR.Server.ExtensionMethods;
using TestShared;

namespace TestServer.Controllers {
    [Route("api/sendtoclient")]
    public class SendToClientController: Controller {


        private ClientManager ClientManager { get; }

        public SendToClientController(ClientManager clientManager) {
            ClientManager = clientManager;
        }

        [HttpPost]
        public async Task<IActionResult> SendToClient([FromBody]JToken query) {


            var result = await ClientManager
                .GetAllClients()
                .WithAttribute("Tag", "BPK")
                .InvokeOneAsync<object>("GetDate", new []{ query }, HttpContext.RequestAborted);
            
            return Ok(result);
        }
        [HttpPost("all")]
        public async Task<IActionResult> SendToAll([FromBody] JToken query) {


            var result = await ClientManager
                .GetAllClients()
                .WithAttribute("Tag", "BPK")
                .InvokeAllAsync<object>("scsm.GetDate", new[] { query }, HttpContext.RequestAborted);

            return Ok(result);
        }

        [HttpPost("name")]
        public async Task<IActionResult> GetName() {


            var cl = ClientManager
                .GetAllClients()
                .FirstOrDefault()?.GetTypedMethods<ITestClientMethods>("ClientTest");

            if (cl == null)
                return NotFound();

           
            

            return Ok(await cl.GetDictionary(DateTime.Now));
        }

    }
}
