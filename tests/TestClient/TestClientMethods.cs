using System;
using System.Collections.Generic;
using System.Text;
using SignalARRR;
using SignalARRR.Attributes;
using SignalARRR.Client;

namespace TestClient {

    [MessageName("Tests")]
    public class TestClientMethods: IClientMethods {


        public DateTime GetDate() {
            
            return DateTime.Now;
        }
    }
}
