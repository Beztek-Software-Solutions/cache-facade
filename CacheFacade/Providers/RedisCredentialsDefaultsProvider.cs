// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Providers
{
    using System;
    using System.Net;
    using StackExchange.Redis.Configuration;

    /// <summary>
    /// StackExchange.Redis defaults that invoke
    /// <see cref="RedisCredentialsProvider"/> for AUTH. User and Password from
    /// the same handshake share one snapshot so an IAM token is not minted twice
    /// (and mismatched) when the client reads both properties.
    /// </summary>
    internal sealed class RedisCredentialsDefaultsProvider : DefaultOptionsProvider
    {
        private readonly RedisCredentialsProvider _credentialsProvider;
        private readonly object _gate = new();
        private RedisCredentials _snapshot;
        private int _remainingReads;

        public RedisCredentialsDefaultsProvider(RedisCredentialsProvider credentialsProvider)
        {
            _credentialsProvider = credentialsProvider
                ?? throw new ArgumentNullException(nameof(credentialsProvider));
        }

        /// <inheritdoc />
        public override bool IsMatch(EndPoint endpoint) => false;

        /// <inheritdoc />
        public override string User => Current().User;

        /// <inheritdoc />
        public override string Password => Current().Password;

        private RedisCredentials Current()
        {
            lock (_gate)
            {
                if (_remainingReads <= 0)
                {
                    _snapshot = _credentialsProvider();
                    // Typical AUTH reads User then Password (or vice versa).
                    _remainingReads = 2;
                }

                _remainingReads--;
                return _snapshot;
            }
        }
    }
}
