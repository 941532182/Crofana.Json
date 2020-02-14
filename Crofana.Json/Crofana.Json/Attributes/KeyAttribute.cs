﻿using System;

namespace Crofana.Json.Attributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class KeyAttribute : Attribute
    {
        public string Name { get; }
        public KeyAttribute(string name)
        {
            Name = name;
        }
    }
}
