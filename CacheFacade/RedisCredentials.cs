// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    /// <summary>
    /// Username / password (or IAM auth token) used when connecting to a
    /// Redis-protocol server (Redis, Valkey, Dragonfly, KeyDB, Garnet).
    /// </summary>
    /// <param name="User">
    /// ACL / ElastiCache user name. Null or empty omits the username (classic
    /// AUTH password, or password-less servers).
    /// </param>
    /// <param name="Password">
    /// Static AUTH password, or a short-lived IAM authentication token for
    /// ElastiCache IAM mode. Null or empty means password-less AUTH (no
    /// requirepass / no-password-required user).
    /// </param>
    public readonly record struct RedisCredentials(string User, string Password);

    /// <summary>
    /// Supplies credentials when the Redis-protocol client authenticates
    /// (including reconnects). Use for password-less servers (return empty
    /// password) or cloud IAM tokens that must be re-minted (e.g. Amazon
    /// ElastiCache for Valkey IAM auth — tokens expire in ~15 minutes).
    /// </summary>
    /// <returns>Credentials for the next AUTH handshake.</returns>
    public delegate RedisCredentials RedisCredentialsProvider();
}
