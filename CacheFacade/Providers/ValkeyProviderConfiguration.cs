// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Configuration for a Valkey-backed cache. Valkey is a Redis-compatible fork and
    /// speaks the Redis protocol, so this reuses the Redis provider and RedLock via the same connection settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Password-less local Valkey:</b> pass an empty <c>password</c> and
    /// <c>useSSL: false</c> (compose / Testcontainers without <c>requirepass</c>).
    /// </para>
    /// <para>
    /// <b>Amazon ElastiCache IAM auth:</b> set <see cref="RedisProviderConfiguration.User"/>
    /// to the ElastiCache IAM-mode user name, <c>useSSL: true</c>, leave
    /// <c>password</c> empty, and set
    /// <see cref="RedisProviderConfiguration.CredentialsProvider"/> to mint a
    /// SigV4 IAM authentication token as <see cref="RedisCredentials.Password"/>.
    /// Tokens expire (~15 minutes); the provider is re-invoked on reconnect AUTH.
    /// </para>
    /// </remarks>
    public class ValkeyProviderConfiguration : RedisProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ValkeyProviderConfiguration"/> class.
        /// </summary>
        /// <param name="endpoint">Valkey server host:port.</param>
        /// <param name="password">
        /// Server password. Empty for password-less hosts or when using
        /// <see cref="RedisProviderConfiguration.CredentialsProvider"/> (IAM).
        /// </param>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key.</param>
        /// <param name="useSSL">Whether to use SSL/TLS for the connection.</param>
        /// <param name="abortConnection">Whether to abort on connect failure.</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        /// <param name="nameIndex">Logical database index (default 0).</param>
        public ValkeyProviderConfiguration(
            string endpoint,
            string password,
            string cacheName,
            bool useSSL = false,
            bool abortConnection = false,
            long timeToLiveMillis = 3600000,
            int nameIndex = 0)
            : base(endpoint, password, cacheName, useSSL, abortConnection, timeToLiveMillis, nameIndex)
        {
            this.ProviderType = CacheProviderType.Valkey;
        }
    }
}
