using System;
using System.Reflection;

namespace Crofana.Json
{
    public class JsonException : ApplicationException
    {

        public JsonException(string msg) : base(msg) { }
        public JsonException(char c) : this($"Invalid character {c}") { }
        public JsonException(int c) : this((char) c) { }

    }
}
