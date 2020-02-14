using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Dynamic;
using Crofana.Json.Attributes;

namespace Crofana.Json
{
    /// <summary>
    /// Json映射器，实现对象和Json字符串的相互映射
    /// </summary>
    public class JsonMapper
    {

        private static Dictionary<JsonBaseType, Dictionary<Type, ParseHandler>> DefaultParseHandlers;
        private static ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo>> KeyAttrBuffer;

        private ConcurrentDictionary<JsonBaseType, ConcurrentDictionary<Type, ParseHandler>> CustomParseHandlers;

        /// <summary>
        /// 是否开启特性，开启后将优先按照特性进行映射，关闭后按照默认元数据进行映射
        /// </summary>
        public bool UseAttribute { get; set; }
        /// <summary>
        /// 是否开启自定义解析器，开启后将优先使用用户定义的解析方法，关闭后只使用默认解析方法
        /// </summary>
        public bool UseCustomParseHandlers { get; set; }

        static JsonMapper()
        {
            KeyAttrBuffer = new ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo>>();
            InitDefaultParseHandlers();
        }

        public JsonMapper() { }

        #region Private methods
        private static void InitDefaultParseHandlers()
        {
            DefaultParseHandlers = new Dictionary<JsonBaseType, Dictionary<Type, ParseHandler>>();
            DefaultParseHandlers[JsonBaseType.Number] = new Dictionary<Type, ParseHandler>();
            DefaultParseHandlers[JsonBaseType.String] = new Dictionary<Type, ParseHandler>();
            DefaultParseHandlers[JsonBaseType.Boolean] = new Dictionary<Type, ParseHandler>();

            DefaultParseHandlers[JsonBaseType.Number][typeof(sbyte)] = delegate (string value)
            {
                return Convert.ToSByte(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(short)] = delegate (string value)
            {
                return Convert.ToInt16(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(int)] = delegate (string value)
            {
                return Convert.ToInt32(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(long)] = delegate (string value)
            {
                return Convert.ToInt64(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(decimal)] = delegate (string value)
            {
                return Convert.ToDecimal(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(byte)] = delegate (string value)
            {
                return Convert.ToByte(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(ushort)] = delegate (string value)
            {
                return Convert.ToUInt16(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(uint)] = delegate (string value)
            {
                return Convert.ToUInt32(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(ulong)] = delegate (string value)
            {
                return Convert.ToUInt64(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(float)] = delegate (string value)
            {
                return Convert.ToSingle(value);
            };
            DefaultParseHandlers[JsonBaseType.Number][typeof(double)] = delegate (string value)
            {
                return Convert.ToDouble(value);
            };

            DefaultParseHandlers[JsonBaseType.String][typeof(string)] = delegate (string value)
            {
                return value;
            };
            DefaultParseHandlers[JsonBaseType.String][typeof(char[])] = delegate (string value)
            {
                return value.ToCharArray();
            };

            DefaultParseHandlers[JsonBaseType.Boolean][typeof(bool)] = delegate (string value)
            {
                return Convert.ToBoolean(value);
            };
        }

        private void InitCustomParseHandlers()
        {
            CustomParseHandlers = new ConcurrentDictionary<JsonBaseType, ConcurrentDictionary<Type, ParseHandler>>();
            CustomParseHandlers[JsonBaseType.Number] = new ConcurrentDictionary<Type, ParseHandler>();
            CustomParseHandlers[JsonBaseType.String] = new ConcurrentDictionary<Type, ParseHandler>();
            CustomParseHandlers[JsonBaseType.Null] = new ConcurrentDictionary<Type, ParseHandler>();
            CustomParseHandlers[JsonBaseType.Boolean] = new ConcurrentDictionary<Type, ParseHandler>();
        }

        private PropertyInfo GetPropertyInfo(Type type, string key)
        {
            if (!UseAttribute)
            {
                return type.GetProperty(key);
            }
            if (!KeyAttrBuffer.ContainsKey(type))
            {
                KeyAttrBuffer[type] = new ConcurrentDictionary<string, PropertyInfo>();
            }
            if (!KeyAttrBuffer[type].ContainsKey(key))
            {
                var props = type.GetProperties();
                bool found = false;
                foreach (var item in props)
                {
                    if (item.GetCustomAttribute<KeyAttribute>()?.Name == key)
                    {
                        KeyAttrBuffer[type][key] = item;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    KeyAttrBuffer[type][key] = type.GetProperty(key);
                }
            }
            return KeyAttrBuffer[type][key];
        }

        private void SetValue(object instance, PropertyInfo property, object value)
        {
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, value);
            }
        }

        private void CheckSyntax(ref Token token, TokenType etype)
        {
            if ((token.type & etype) == 0)
            {
                throw new JsonException("Json syntax error");
            }
        }

        private string ParseKey(ParseContext ctx)
        {
            return ctx.Current.value;
        }

        private object ParseObject(ParseContext ctx, Type ttype)
        {
            var instance = Activator.CreateInstance(ttype);
            var token = ctx.Read();
            if (token.type == TokenType.ObjectEnd)
            {
                return instance;
            }
            string key;
            PropertyInfo prop;
            object value;
            while (true)
            {
                CheckSyntax(ref token, TokenType.String);
                key = ParseKey(ctx);
                prop = GetPropertyInfo(ttype, key);
                token = ctx.Read();
                CheckSyntax(ref token, TokenType.Colon);
                token = ctx.Read();
                switch (token.type)
                {
                    case TokenType.ObjectStart:
                        value = ParseObject(ctx, prop.PropertyType);
                        break;
                    case TokenType.ArrayStart:
                        value = ParseArray(ctx, prop.PropertyType);
                        break;
                    case TokenType.Number:
                        value = ParseBaseType(ctx, JsonBaseType.Number, prop.PropertyType);
                        break;
                    case TokenType.String:
                        value = ParseBaseType(ctx, JsonBaseType.String, prop.PropertyType);
                        break;
                    case TokenType.Constant:
                        JsonBaseType btype;
                        if (token.value == "null")
                        {
                            btype = JsonBaseType.Null;
                        } else if (token.value == "true" || token.value == "false")
                        {
                            btype = JsonBaseType.Boolean;
                        } else
                        {
                            throw new JsonException("Unknown error at method 'ParseObject'");
                        }
                        value = ParseBaseType(ctx, btype, prop.PropertyType);
                        break;
                    default:
                        throw new JsonException("Json syntax error");
                }
                SetValue(instance, prop, value);
                token = ctx.Read();
                CheckSyntax(ref token, TokenType.Comma | TokenType.ObjectEnd);
                if (token.type == TokenType.Comma)
                {
                    token = ctx.Read();
                } else
                {
                    return instance;
                }
            }
        }

        private object ParseArray(ParseContext ctx, Type ttype)
        {
            if (!ttype.IsArray)
            {
                throw new JsonException("Target type is not Array");
            }
            var etype = ttype.GetElementType();
            var token = ctx.Read();
            if (token.type == TokenType.ArrayEnd)
            {
                return Array.CreateInstance(etype, 0);
            }
            object value;
            var elements = new List<object>();
            while (true)
            {
                CheckSyntax(ref token, TokenType.ObjectStart |
                                       TokenType.ArrayStart |
                                       TokenType.Number |
                                       TokenType.String |
                                       TokenType.Constant);
                switch (token.type)
                {
                    case TokenType.ObjectStart:
                        value = ParseObject(ctx, etype);
                        break;
                    case TokenType.ArrayStart:
                        value = ParseArray(ctx, etype);
                        break;
                    case TokenType.Number:
                        value = ParseBaseType(ctx, JsonBaseType.Number, etype);
                        break;
                    case TokenType.String:
                        value = ParseBaseType(ctx, JsonBaseType.String, etype);
                        break;
                    case TokenType.Constant:
                        JsonBaseType btype;
                        if (token.value == "null")
                        {
                            btype = JsonBaseType.Null;
                        } else if (token.value == "true" || token.value == "false")
                        {
                            btype = JsonBaseType.Boolean;
                        } else
                        {
                            throw new JsonException("Unknown error at method 'ParseObject'");
                        }
                        value = ParseBaseType(ctx, btype, etype);
                        break;
                    default:
                        throw new JsonException("Json syntax error");
                }
                elements.Add(value);
                token = ctx.Read();
                CheckSyntax(ref token, TokenType.Comma | TokenType.ArrayEnd);
                if (token.type == TokenType.Comma)
                {
                    token = ctx.Read();
                } else
                {
                    var instance = Array.CreateInstance(etype, elements.Count);
                    for (int i = 0; i < elements.Count; i++)
                    {
                        instance.SetValue(elements[i], i);
                    }
                    return instance;
                }
            }
        }

        private object ParseBaseType(ParseContext ctx, JsonBaseType jbtype, Type ttype)
        {
            if (UseCustomParseHandlers && CustomParseHandlers != null && CustomParseHandlers[jbtype].ContainsKey(ttype))
            {
                return CustomParseHandlers[jbtype][ttype].Invoke(ctx.Current.value);
            } else if (jbtype == JsonBaseType.Null)
            {
                if (ttype.IsClass)
                {
                    return null;
                } else
                {
                    throw new JsonException("Cannot assign value type to null");
                }
            } else if (DefaultParseHandlers[jbtype].ContainsKey(ttype))
            {
                return DefaultParseHandlers[jbtype][ttype].Invoke(ctx.Current.value);
            } else
            {
                throw new JsonException($"Cannot parse {Enum.GetName(typeof(JsonBaseType), jbtype)} to {ttype.Name}");
            }
        }

        private dynamic ParseObject(ParseContext ctx)
        {
            dynamic instance = new ExpandoObject();
            var token = ctx.Read();
            if (token.type == TokenType.ObjectEnd)
            {
                return instance;
            }
            string key;
            object value;
            while (true)
            {
                CheckSyntax(ref token, TokenType.String);
                key = ParseKey(ctx);
                token = ctx.Read();
                CheckSyntax(ref token, TokenType.Colon);
                token = ctx.Read();
                switch (token.type)
                {
                    case TokenType.ObjectStart:
                        value = ParseObject(ctx);
                        break;
                    case TokenType.ArrayStart:
                        value = ParseArray(ctx);
                        break;
                    case TokenType.Number:
                        value = ParseBaseType(ctx, JsonBaseType.Number);
                        break;
                    case TokenType.String:
                        value = ParseBaseType(ctx, JsonBaseType.String);
                        break;
                    case TokenType.Constant:
                        JsonBaseType btype;
                        if (token.value == "null")
                        {
                            btype = JsonBaseType.Null;
                        } else if (token.value == "true" || token.value == "false")
                        {
                            btype = JsonBaseType.Boolean;
                        } else
                        {
                            throw new JsonException("Unknown error at method 'ParseObject'");
                        }
                        value = ParseBaseType(ctx, btype);
                        break;
                    default:
                        throw new JsonException("Json syntax error");
                }
                ((IDictionary<string, object>) instance)[key] = value;
                token = ctx.Read();
                CheckSyntax(ref token, TokenType.Comma | TokenType.ObjectEnd);
                if (token.type == TokenType.Comma)
                {
                    token = ctx.Read();
                } else
                {
                    return instance;
                }
            }
        }

        private dynamic ParseArray(ParseContext ctx)
        {
            var token = ctx.Read();
            if (token.type == TokenType.ArrayEnd)
            {
                return Array.CreateInstance(typeof(object), 0);
            }
            object value;
            List<object> elements = new List<object>();
            while (true)
            {
                CheckSyntax(ref token, TokenType.ObjectStart |
                                       TokenType.ArrayStart |
                                       TokenType.Number |
                                       TokenType.String |
                                       TokenType.Constant);
                switch (token.type)
                {
                    case TokenType.ObjectStart:
                        value = ParseObject(ctx);
                        break;
                    case TokenType.ArrayStart:
                        value = ParseArray(ctx);
                        break;
                    case TokenType.Number:
                        value = ParseBaseType(ctx, JsonBaseType.Number);
                        break;
                    case TokenType.String:
                        value = ParseBaseType(ctx, JsonBaseType.String);
                        break;
                    case TokenType.Constant:
                        JsonBaseType btype;
                        if (token.value == "null")
                        {
                            btype = JsonBaseType.Null;
                        } else if (token.value == "true" || token.value == "false")
                        {
                            btype = JsonBaseType.Boolean;
                        } else
                        {
                            throw new JsonException("Unknown error at method 'ParseObject'");
                        }
                        value = ParseBaseType(ctx, btype);
                        break;
                    default:
                        throw new JsonException("Json syntax error");
                }
                elements.Add(value);
                token = ctx.Read();
                CheckSyntax(ref token, TokenType.Comma | TokenType.ArrayEnd);
                if (token.type == TokenType.Comma)
                {
                    token = ctx.Read();
                } else
                {
                    dynamic instance = Array.CreateInstance(typeof(object), elements.Count);
                    for (int i = 0; i < elements.Count; i++)
                    {
                        instance[i] = elements[i];
                    }
                    return instance;
                }
            }
        }

        private dynamic ParseBaseType(ParseContext ctx, JsonBaseType jbtype)
        {
            switch (jbtype)
            {
                case JsonBaseType.Number:
                    return DefaultParseHandlers[jbtype][typeof(long)].Invoke(ctx.Current.value);
                case JsonBaseType.String:
                    return DefaultParseHandlers[jbtype][typeof(string)].Invoke(ctx.Current.value);
                case JsonBaseType.Null:
                    return null;
                case JsonBaseType.Boolean:
                    return DefaultParseHandlers[jbtype][typeof(bool)].Invoke(ctx.Current.value);
                default:
                    throw new JsonException("Unknown error");
            }
        }
        #endregion

        public dynamic ToObject(string json)
        {
            var ctx = new ParseContext(Lexer.Analyze(json));
            dynamic instance;
            Token token;
            switch ((token = ctx.Read()).type)
            {
                case TokenType.ObjectStart:
                    instance = ParseObject(ctx);
                    break;
                case TokenType.ArrayStart:
                    instance = ParseArray(ctx);
                    break;
                case TokenType.Number:
                    instance = ParseBaseType(ctx, JsonBaseType.Number);
                    break;
                case TokenType.String:
                    instance = ParseBaseType(ctx, JsonBaseType.String);
                    break;
                case TokenType.Constant:
                    switch (token.value)
                    {
                        case "null":
                            instance = ParseBaseType(ctx, JsonBaseType.Null);
                            break;
                        case "true":
                        case "false":
                            instance = ParseBaseType(ctx, JsonBaseType.Boolean);
                            break;
                        default:
                            throw new JsonException("Json syntax error");
                    }
                    break;
                default:
                    throw new JsonException("Json syntax error");
            }
            if (ctx.Read().type == TokenType.End)
            {
                return instance;
            } else
            {
                throw new JsonException("Json syntax error");
            }
        }

        public dynamic ToObject(TextReader reader)
        {
            return ToObject(reader.ReadToEnd());
        }

        public object ToObject(Type type, string json)
        {
            var ctx = new ParseContext(Lexer.Analyze(json));
            object instance;
            Token token;
            switch ((token = ctx.Read()).type)
            {
                case TokenType.ObjectStart:
                    instance = ParseObject(ctx, type);
                    break;
                case TokenType.ArrayStart:
                    instance = ParseArray(ctx, type);
                    break;
                case TokenType.Number:
                    instance = ParseBaseType(ctx, JsonBaseType.Number, type);
                    break;
                case TokenType.String:
                    instance = ParseBaseType(ctx, JsonBaseType.String, type);
                    break;
                case TokenType.Constant:
                    switch (token.value)
                    {
                        case "null":
                            instance = ParseBaseType(ctx, JsonBaseType.Null, type);
                            break;
                        case "true":
                        case "false":
                            instance = ParseBaseType(ctx, JsonBaseType.Boolean, type);
                            break;
                        default:
                            throw new JsonException("Json syntax error");
                    }
                    break;
                default:
                    throw new JsonException("Json syntax error");
            }
            if (ctx.Read().type == TokenType.End)
            {
                return instance;
            } else
            {
                throw new JsonException("Json syntax error");
            }
        }

        public object ToObject(Type type, TextReader reader)
        {
            return ToObject(type, reader.ReadToEnd());
        }

        public T ToObject<T>(string json)
        {
            return (T) ToObject(typeof(T), json);
        }

        public T ToObject<T>(TextReader reader)
        {
            return ToObject<T>(reader.ReadToEnd());
        }

        public string ToJson(object obj)
        {
            throw new NotImplementedException();
        }

        public void RegisterParseHandler(JsonBaseType jbtype, Type ttype, ParseHandler handler)
        {
            if (CustomParseHandlers == null)
            {
                InitCustomParseHandlers();
            }
            CustomParseHandlers[jbtype][ttype] = handler;
        }

        public bool UnregisterParseHandler(JsonBaseType jbtype, Type ttype)
        {
            ParseHandler p;
            return CustomParseHandlers[jbtype].TryRemove(ttype, out p);
        }

        private class ParseContext
        {
            private int pointer;
            private Token[] tokens;

            public ParseContext(Token[] tokens)
            {
                this.tokens = tokens;
            }

            public ref Token Read()
            {
                if (pointer >= tokens.Length)
                {
                    throw new JsonException("Token list iterator is at last");
                }
                return ref tokens[pointer++];
            }

            public ref Token Current => ref tokens[pointer - 1];

            public ref Token Next => ref tokens[pointer];

            public bool IsHead => pointer == 0;

            public bool IsLast => pointer == tokens.Length - 1;
        }

    }

    public delegate object ParseHandler(string value);

    public enum JsonBaseType
    {
        None,
        Number,
        String,
        Null,
        Boolean,
    }
}
