using System;
using Newtonsoft.Json;

namespace FaunaDB.Extensions
{
    public class ObjectSerializer : JsonConverter
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include };

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JsonSerializer.Create(Settings).Serialize(writer, value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize(reader, objectType);
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(Language.ObjO).IsAssignableFrom(objectType);
        }
    }
}