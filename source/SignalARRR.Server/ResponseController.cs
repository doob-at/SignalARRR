using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace SignalARRR.Server {
    
    public class ResponseController<T>: Controller where T : HARRR {

        public ResponseController() {

        }
    }
}
