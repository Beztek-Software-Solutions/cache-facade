// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    /// <summary>
    /// Username / password (or IAM auth token) used when connecting to a
    /// Redis-protocol server (Redis, Valkey, Dragonfly, KeyDB, Garnet).
    /// </summary>
    /// <remarks>
    /// Prefer the three-argument form with <see cref="ExpiresAt"/> for short-lived
    /// cloud IAM tokens so the facade can cache until near expiry. The two-argument
    /// form remains for compatibility and remints on every AUTH handshake.
    /// Static AUTH secrets and password-less hosts should use the configuration
    /// constructor <c>password</c> and omit <see cref="RedisCredentialsProvider"/>.
    /// </remarks>
    /// <param name="User">
    /// ACL / ElastiCache user name. Null or empty omits the username (classic
    /// AUTH password, or password-less servers).
    /// </param>
    /// <param name="Password">
    /// Static AUTH password, or a short-lived IAM authentication token.
    /// Null or empty means password-less AUTH when used without a provider.
    /// </param>
    /// <param name="ExpiresAt">
    /// UTC expiry for short-lived tokens. When set, credentials are cached until
    /// <c>ExpiresAt - PasswordRefreshSkew</c>. When null (two-argument constructor),
    /// the provider remints on every AUTH.
    /// </param>
    public readonly record struct RedisCredentials(
        string User,
        string Password,
        DateTimeOffset? ExpiresAt = null);

    /// <summary>
    /// Supplies credentials when the Redis-protocol client authenticates
    /// (including reconnects). Use for cloud IAM tokens that must be re-minted
    /// (e.g. Amazon ElastiCache for Valkey IAM auth — tokens expire in ~15 minutes).
    /// Prefer returning <see cref="RedisCredentials"/> with <c>ExpiresAt</c> set
    /// so the facade caches until near expiry.
    /// </summary>
    /// <returns>Credentials for the next AUTH handshake.</returns>
    public delegate RedisCredentials RedisCredentialsProvider();
}
