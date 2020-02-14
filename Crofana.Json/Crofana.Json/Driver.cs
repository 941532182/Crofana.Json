using System.Text;

namespace Crofana.Json
{
    public static class Driver
    {

        public static string Analyze(string json)
        {
            var tokens = Lexer.Analyze(json);
            var sb = new StringBuilder();
            foreach (var item in tokens)
            {
                sb.Append(item.ToString());
                sb.Append("\n");
            }
            return sb.ToString();
        }

    }
}
