// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Defines the configuration needed for a Redis-backed cache provider.
    /// </summary>
    public class RedisProviderConfiguration : ICacheProviderConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RedisProviderConfiguration"/> class.
        /// </summary>
        /// <param name="endpoint">Redis server host:port.</param>
        /// <param name="password">
        /// Redis server password. May be empty for password-less servers (no
        /// <c>requirepass</c>) or when <see cref="CredentialsProvider"/> supplies
        /// credentials (e.g. ElastiCache IAM tokens).
        /// </param>
        /// <param name="cacheName">Logical cache name used as the <see cref="CacheFactory"/> registry key.</param>
        /// <param name="useSSL">Whether to use SSL/TLS for the Redis connection.</param>
        /// <param name="abortConnection">Whether to abort on connect failure (StackExchange.Redis <c>AbortOnConnectFail</c>).</param>
        /// <param name="timeToLiveMillis">TTL for cached entries in milliseconds (default 1 hour).</param>
        /// <param name="nameIndex">Redis database index (default 0).</param>
        public RedisProviderConfiguration(string endpoint, string password, string cacheName, bool useSSL = true, bool abortConnection = false, long timeToLiveMillis = 3600000, int nameIndex = 0)
        {
            this.CacheName = cacheName;
            this.Endpoint = endpoint;
            this.Password = password ?? string.Empty;
            this.UseSSL = useSSL;
            this.AbortConnection = abortConnection;

            this.NameIndex = nameIndex;
            this.TimeToLiveMillis = timeToLiveMillis;
            this.ProviderType = CacheProviderType.Redis;
            this.DistributedLockKind = RedisDistributedLockKind.RedLock;
        }

        /// <summary>Redis server endpoint (host:port).</summary>
        public string Endpoint { get; }

        /// <summary>Whether SSL/TLS is enabled for the connection.</summary>
        public bool UseSSL { get; }

        /// <summary>Whether connection attempts abort on failure.</summary>
        public bool AbortConnection { get; }

        /// <summary>
        /// Static Redis / Valkey AUTH password. Empty for password-less hosts.
        /// Ignored when <see cref="CredentialsProvider"/> is set.
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// Optional ACL / ElastiCache user name (Redis 6+ <c>AUTH user password</c>).
        /// Required for Amazon ElastiCache IAM authentication. Ignored when
        /// <see cref="CredentialsProvider"/> returns a user.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// When set, invoked to obtain username/password (or IAM token) for each
        /// AUTH handshake, including reconnects. Prefer this over a static
        /// <see cref="Password"/> for short-lived cloud credentials. See README
        /// “Password-less and IAM authentication”.
        /// </summary>
        public RedisCredentialsProvider CredentialsProvider { get; set; }

        /// <inheritdoc />
        public CacheProviderType ProviderType { get; set; }

        /// <summary>Redis database (logical DB) index for this cache partition.</summary>
        public int NameIndex { get; set; }

        /// <inheritdoc />
        public string CacheName { get; set; }

        /// <inheritdoc />
        public long TimeToLiveMillis { get; set; }

        /// <summary>
        /// Lock strategy for this Redis-protocol backend.
        /// Use <see cref="RedisDistributedLockKind.Token"/> when RedLock/Lua is unavailable (e.g. Garnet).
        /// </summary>
        public RedisDistributedLockKind DistributedLockKind { get; set; }

        /// <summary>
        /// A comma-separated list of name=value pairs for the underlying Redis client.
        /// Explicit properties on this class override overlapping values in this string
        /// (for example <see cref="UseSSL"/> overrides <c>ssl=true</c>).
        /// </summary>
        public string Options { get; set; }
    }
}
