// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public static class SerializationUtil
    {

        private static readonly JsonSerializerOptions JsonSerializerOptions = GetJsonSerializerOptions();

        public static byte[] Serialize(SerializationType serializationType, object cacheable)
        {
            if (cacheable == null)
                return null;

            if (serializationType == SerializationType.Json)
            {
                return StringToByte(JsonSerialize(cacheable));
            }

            throw new NotSupportedException($"Serialization type {serializationType} is not supported");
        }

        public static T Deserialize<T>(SerializationType serializationType, byte[] data)
        {
            if (data == null)
                return default(T);

            if (serializationType == SerializationType.Json)
            {
                return JsonDeserialize<T>(ByteToString(data));
            }

            throw new NotSupportedException($"Serialization type {serializationType} is not supported");
        }

        public static string JsonSerialize(object cacheable)
        {
            if (cacheable == null)
                return null;

            return JsonSerializer.Serialize(cacheable, JsonSerializerOptions);
        }

        public static T JsonDeserialize<T>(string data)
        {
            if (data == null)
                return default(T);

            return JsonSerializer.Deserialize<T>(data);
        }

        public static string ByteToString(byte[] data)
        {
            if (data == null)
                return null;

            return Encoding.ASCII.GetString(data);
        }

        public static byte[] StringToByte(string data)
        {
            if (data == null)
                return null;

            return Encoding.ASCII.GetBytes(data);
        }

        // Used by inline static initialization
        private static JsonSerializerOptions GetJsonSerializerOptions()
        {
            JsonSerializerOptions tmpOptions = new JsonSerializerOptions {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            return tmpOptions;
        }
    }
}
