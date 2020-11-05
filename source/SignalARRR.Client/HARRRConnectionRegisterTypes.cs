using System;
using System.Collections.Generic;
using System.Text;

namespace SignalARRR.Client {
    public partial class HARRRConnection {

        


        public void RegisterType<TInterface, TClass>() where TClass : class, TInterface { 
            _harrrContext.MessageHandler.RegisterType<TInterface, TClass>();
        }

       
        public void RegisterType<TInterface, TClass>(TClass instance) where TClass : class, TInterface {

            _harrrContext.MessageHandler.RegisterType<TInterface, TClass>(instance);
        }

        public void RegisterType<TInterface, TClass>(Func<IServiceProvider, TClass> factory)
            where TClass : class, TInterface {

            _harrrContext.MessageHandler.RegisterType<TInterface, TClass>(factory);
        }


    }
}
