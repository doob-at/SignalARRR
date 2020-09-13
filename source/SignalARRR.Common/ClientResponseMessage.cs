using System;
using System.Collections.Generic;
using System.Text;

namespace SignalARRR {
    public class ClientResponseMessage {

        public Guid Id { get; }

        public object PayLoad { get; set; }

        public string ErrorMessage { get; set; }

        public ClientResponseMessage(Guid id) {
            Id = id;
        }

    }
}
