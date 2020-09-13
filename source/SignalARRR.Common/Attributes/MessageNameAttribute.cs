﻿using System;

namespace SignalARRR.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class MessageNameAttribute : Attribute
    {
        public string Name { get; }

        public MessageNameAttribute(string @namespace)
        {
            Name = @namespace;
        }
        
    }
}
