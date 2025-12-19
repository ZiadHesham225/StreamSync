using System.Text.Json.Serialization;

namespace StreamSync.Extensions
{
    public static class JsonSerializationExtensions
    {
        public static void ConfigureJsonOptions(Action<System.Text.Json.JsonSerializerOptions> configure)
        {
            var options = new System.Text.Json.JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            configure(options);
        }

        public static void AddCommonJsonOptions(this System.Text.Json.JsonSerializerOptions options)
        {
            options.Converters.Add(new JsonStringEnumConverter());
            options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        }
    }
}
