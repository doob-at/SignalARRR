using System;
using System.Reflection;

namespace doob.SignalARRR.Common.Interfaces {
    public interface ISignalARRRInterfaceCollection {
        void RegisterType<TInterface, TClass>() where TClass : class, TInterface;
        void RegisterType<TInterface, TClass>(TClass instance) where TClass : class, TInterface;
        void RegisterType<TInterface, TClass>(Func<IServiceProvider, TClass> factory) where TClass : class, TInterface;

        void RegisterInterface(Type interfaceType, Type instanceType);
        void RegisterInterface(Type interfaceType, object instance);
        void RegisterInterface(Type interfaceType, Func<IServiceProvider, object> factory);

        (Delegate Factory, MethodInfo MethodInfo) GetInvokeInformation(string name);
    }
}
