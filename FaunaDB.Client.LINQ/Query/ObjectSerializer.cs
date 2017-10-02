﻿using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FaunaDB.LINQ.Query
{
    public class ObjectSerializer : JsonConverter
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include };

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var wrapped = ((Language.ObjO) value).O;
            writer.WriteStartObject();

            var prop = new JProperty("object", JObject.FromObject(wrapped, JsonSerializer.CreateDefault(Settings)));
            prop.WriteTo(writer);

            writer.WriteEndObject();
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