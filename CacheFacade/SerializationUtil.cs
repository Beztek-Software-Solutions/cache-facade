// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serialization helpers used by cache providers and write-behind payloads.
    /// Only <see cref="SerializationType.Json"/> is supported.
    /// </summary>
    public static class SerializationUtil
    {

        private static readonly JsonSerializerOptions JsonSerializerOptions = GetJsonSerializerOptions();

        /// <summary>
        /// Serializes an object to bytes for the given serialization type.
        /// </summary>
        /// <param name="serializationType">Must be <see cref="SerializationType.Json"/>.</param>
        /// <param name="cacheable">Object to serialize; <c>null</c> returns <c>null</c>.</param>
        /// <returns>ASCII-encoded JSON bytes, or <c>null</c>.</returns>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="serializationType"/> is not JSON.</exception>
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

        /// <summary>
        /// Deserializes bytes to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="serializationType">Must be <see cref="SerializationType.Json"/>.</param>
        /// <param name="data">ASCII-encoded JSON; <c>null</c> returns <c>default(T)</c>.</param>
        /// <returns>Deserialized value.</returns>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="serializationType"/> is not JSON.</exception>
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

        /// <summary>
        /// Serializes an object to a JSON string (null properties omitted).
        /// </summary>
        /// <param name="cacheable">Object to serialize; <c>null</c> returns <c>null</c>.</param>
        /// <returns>JSON string, or <c>null</c>.</returns>
        public static string JsonSerialize(object cacheable)
        {
            if (cacheable == null)
                return null;

            return JsonSerializer.Serialize(cacheable, JsonSerializerOptions);
        }

        /// <summary>
        /// Deserializes a JSON string to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="data">JSON string; <c>null</c> returns <c>default(T)</c>.</param>
        /// <returns>Deserialized value.</returns>
        public static T JsonDeserialize<T>(string data)
        {
            if (data == null)
                return default(T);

            return JsonSerializer.Deserialize<T>(data);
        }

        /// <summary>
        /// Decodes ASCII bytes to a string.
        /// </summary>
        /// <param name="data">Byte array; <c>null</c> returns <c>null</c>.</param>
        /// <returns>Decoded string, or <c>null</c>.</returns>
        public static string ByteToString(byte[] data)
        {
            if (data == null)
                return null;

            return Encoding.ASCII.GetString(data);
        }

        /// <summary>
        /// Encodes a string as ASCII bytes.
        /// </summary>
        /// <param name="data">String; <c>null</c> returns <c>null</c>.</param>
        /// <returns>ASCII bytes, or <c>null</c>.</returns>
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
