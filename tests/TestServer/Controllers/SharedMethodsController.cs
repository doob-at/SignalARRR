using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using Reflectensions.ExtensionMethods;
using SignalARRR;
using SignalARRR.Server;
using SignalARRR.Server.ExtensionMethods;
using TestShared;

namespace TestServer.Controllers {
    [Route("api/shared")]
    public class SharedMethodsController : Controller {


        private ClientManager ClientManager { get; }

        public SharedMethodsController(ClientManager clientManager) {
            ClientManager = clientManager;
        }

        
        [HttpGet]
        public async Task<IActionResult> Test1() {

           
            var cl = ClientManager.GetAllClients().FirstOrDefault()?.GetTypedMethods<ISharedMethods>();

            if (cl == null)
                return BadRequest("No client");
            
            
            return Ok(cl.GetCurrentDateTime());
        }

    }
    
}
