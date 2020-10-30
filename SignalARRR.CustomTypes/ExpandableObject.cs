using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;

namespace SignalARRR.CustomTypes {

    public abstract class ExpandableBaseObject : DynamicObject {

        private readonly object _instance;
        private readonly Type _instanceType;

        private PropertyInfo[] _instancePropertyInfo;
        private PropertyInfo[] InstancePropertyInfo {
            get {
                if (_instancePropertyInfo == null && _instance != null) {
                    _instancePropertyInfo = _instance
                        .GetType()
                        .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase)
                        .ToArray();
                }
                return _instancePropertyInfo;
            }
        }


        public Dictionary<string, object> __Properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);


        protected ExpandableBaseObject() {
            _instance = this;
            _instanceType = _instance.GetType();

        }


        protected ExpandableBaseObject(object instance) {
            _instance = instance;
            if (instance != null) {
                _instanceType = _instance.GetType();
            }
        }


        public override IEnumerable<string> GetDynamicMemberNames() {
            foreach (var prop in GetProperties()) {
                yield return prop.Key;
            }
        }


        public override bool TryGetMember(GetMemberBinder binder, out object result) {

            if (__Properties.Keys.Contains(binder.Name)) {
                result = __Properties[binder.Name];
                return true;
            }

            if (_instance != null) {
                try {
                    if (TryGetProperty(_instance, binder.Name, out result)) {
                        return true;
                    }
                } catch {
                    // ignored
                }
            }

            result = null;
            return false;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value) {

            if (_instance != null) {
                try {
                    if (TrySetProperty(_instance, binder.Name, value)) {
                        return true;
                    }
                } catch {
                    // ignored
                }
            }

            __Properties[binder.Name] = value;
            return true;
        }


        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            if (_instance != null) {
                try {
                    if (TryInvokeMethod(_instance, binder.Name, args, out result)) {
                        return true;
                    }
                } catch {
                    // ignored
                }
            }

            result = null;
            return false;
        }


        protected bool TryGetProperty(object instance, string name, out object result) {

            instance = instance ?? this;

            var memberInfo = _instanceType
                .GetMember(name, BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.IgnoreCase).FirstOrDefault();

            if (memberInfo?.MemberType == MemberTypes.Property) {
                result = ((PropertyInfo)memberInfo).GetValue(instance, null);
                return true;
            }

            result = null;
            return false;
        }


        protected bool TrySetProperty(object instance, string name, object value) {

            instance = instance ?? this;

            var memberInfo = _instanceType
                .GetMember(name, BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.IgnoreCase).FirstOrDefault();

            if (memberInfo?.MemberType == MemberTypes.Property) {
                ((PropertyInfo)memberInfo).SetValue(instance, value, null);
                return true;
            }

            //var miArray = _instanceType.GetMember(name, BindingFlags.Public | BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.IgnoreCase)
            //    //.Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() == null)
            //    .ToArray();
            //if (miArray != null && miArray.Length > 0) {
            //    var mi = miArray[0];
            //    if (mi.MemberType == MemberTypes.Property) {
            //        ((PropertyInfo)mi).SetValue(_instance, value, null);
            //        return true;
            //    }
            //}
            return false;
        }


        protected bool TryInvokeMethod(object instance, string name, object[] args, out object result) {

            instance = instance ?? this;

            var memberInfo = _instanceType
                .GetMember(name, BindingFlags.Public | BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.IgnoreCase).FirstOrDefault();

            if (memberInfo?.MemberType == MemberTypes.Method) {
                result = ((MethodInfo)memberInfo).Invoke(instance, args);
                return true;
            }

            // Look at the instanceType
            //var miArray = _instanceType.GetMember(name,
            //                        BindingFlags.InvokeMethod |
            //                        BindingFlags.Public | BindingFlags.Instance);



            //if (miArray != null && miArray.Length > 0) {
            //    var mi = miArray[0] as MethodInfo;
            //    result = mi.Invoke(_instance, args);
            //    return true;
            //}

            result = null;
            return false;
        }


        public object this[string key] {
            get {

                if (TryGetProperty(_instance, key, out var result)) {
                    return result;
                }

                if (__Properties.TryGetValue(key, out var prop)) {
                    return prop;
                } 
                
                throw new KeyNotFoundException(key);
            }
            set {

                if (!TrySetProperty(_instance, key, value)) {
                    __Properties[key] = value;
                }

            }
        }


        public IEnumerable<KeyValuePair<string, object>> GetProperties(bool includeInstanceProperties = true) {

            if (includeInstanceProperties && _instance != null) {
                foreach (var prop in InstancePropertyInfo) {
                    yield return new KeyValuePair<string, object>(prop.Name, prop.GetValue(_instance, null));
                }
            }

            foreach (var key in __Properties.Keys) {
                yield return new KeyValuePair<string, object>(key, __Properties[key]);
            }

        }


        public bool Contains(KeyValuePair<string, object> item, bool includeInstanceProperties = false) {
            var res = __Properties.ContainsKey(item.Key);
            if (res) {
                return true;
            }
                

            if (includeInstanceProperties && _instance != null) {
                foreach (var prop in InstancePropertyInfo) {
                    if (prop.Name == item.Key) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool ContainsKey(string key, bool includeInstanceProperties = false) {

            var res = __Properties.ContainsKey(key);
            if (res) {
                return true;
            }
            
            if (includeInstanceProperties && _instance != null) {
                foreach (var prop in InstancePropertyInfo) {
                    if (prop.Name == key) {
                        return true;
                    }
                }
            }

            return false;

        }

        public bool IsFixedProperty(string key) {

            foreach (var prop in InstancePropertyInfo) {
                if (prop.Name == key)
                    return true;
            }

            return false;
        }

    }

    public class ExpandableObject : ExpandableBaseObject {

    }
}
