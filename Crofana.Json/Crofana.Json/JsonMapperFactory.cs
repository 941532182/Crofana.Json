namespace Crofana.Json
{
    /// <summary>
    /// JsonMapper工厂，提供常用几种配置的JsonMapper
    /// </summary>
    public static class JsonMapperFactory
    {

        /// <summary>
        /// 返回一个简单Json映射器
        /// </summary>
        /// <returns></returns>
        public static JsonMapper GetPlainMapper()
        {
            return new JsonMapper();
        }

        /// <summary>
        /// 返回一个高级Json映射器，可以使用特性和自定义解析器
        /// </summary>
        /// <returns></returns>
        public static JsonMapper GetAdvancedMapper()
        {
            var mapper = new JsonMapper();
            mapper.UseAttribute = true;
            mapper.UseCustomParseHandlers = true;
            return mapper;
        }

    }
}
