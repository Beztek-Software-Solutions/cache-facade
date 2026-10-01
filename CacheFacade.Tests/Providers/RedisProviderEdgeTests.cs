// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests
{
    using System;
    using Beztek.Facade.Cache;
    using Beztek.Facade.Cache.Providers;
    using Moq;
    using NUnit.Framework;
    using StackExchange.Redis;

    [TestFixture]
    public class RedisProviderEdgeTests
    {
        [Test]
        public void Clear_WithoutMultiplexer_ReturnsFalse()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Clear(), Is.False);
        }

        [Test]
        public void Remove_WhenKeyMissing_ReturnsDefault()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database.Setup(d => d.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(false);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Remove<TestCacheable>("missing"), Is.Null);
        }

        [Test]
        public void Remove_WhenValueNull_ReturnsDefaultWithoutDelete()
        {
            var database = new Mock<IDatabase>(MockBehavior.Strict);
            database.Setup(d => d.KeyExists(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(true);
            database.Setup(d => d.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).Returns(RedisValue.Null);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Remove<TestCacheable>("k"), Is.Null);
            database.Verify(d => d.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never);
        }

        [Test]
        public void BuildConnectionKey_IncludesIdentityFields()
        {
            var config = new RedisProviderConfiguration(
                "127.0.0.1:6379",
                "secret",
                "orders",
                useSSL: true,
                abortConnection: false,
                timeToLiveMillis: 1000)
            {
                User = "app-user",
                Options = "connectTimeout=100",
            };
            string key = RedisProvider.BuildConnectionKey(config);
            Assert.That(key, Does.Contain("127.0.0.1:6379"));
            Assert.That(key, Does.Contain("app-user"));
            Assert.That(key, Does.Contain("secret"));
            Assert.That(key, Does.Contain("True"));
            Assert.That(key, Does.Contain("False"));
            Assert.That(key, Does.Contain("connectTimeout=100"));
        }

        [Test]
        public void BuildConnectionKey_CredentialsProvider_DoesNotEmbedToken()
        {
            var config = new RedisProviderConfiguration("127.0.0.1:6379", "", "orders", useSSL: true)
            {
                User = "iam-user",
                CredentialsProvider = () => new RedisCredentials("iam-user", "rotating-token-value"),
            };

            string key = RedisProvider.BuildConnectionKey(config);
            Assert.That(key, Does.Contain("credentials-provider"));
            Assert.That(key, Does.Not.Contain("rotating-token-value"));
        }

        [Test]
        public void BuildConfigurationOptions_PasswordLess_AllowsEmptyPassword()
        {
            var config = new RedisProviderConfiguration("127.0.0.1:6379", "", "orders", useSSL: false);
            var options = RedisProvider.BuildConfigurationOptions(config);
            Assert.That(options.Password, Is.Empty);
            Assert.That(options.Ssl, Is.False);
            Assert.That(options.User, Is.Null.Or.Empty);
        }

        [Test]
        public void BuildConfigurationOptions_SetsUserAndPassword()
        {
            var config = new RedisProviderConfiguration("127.0.0.1:6379", "secret", "orders", useSSL: true)
            {
                User = "acl-user",
            };
            var options = RedisProvider.BuildConfigurationOptions(config);
            Assert.That(options.User, Is.EqualTo("acl-user"));
            Assert.That(options.Password, Is.EqualTo("secret"));
            Assert.That(options.Ssl, Is.True);
        }

        [Test]
        public void BuildConfigurationOptions_CredentialsProvider_UsesDefaultsForRefreshableAuth()
        {
            var calls = 0;
            var config = new ValkeyProviderConfiguration(
                "master.example.cache.amazonaws.com:6379",
                password: "",
                cacheName: "orders",
                useSSL: true)
            {
                User = "grasp-api",
                CredentialsProvider = () =>
                {
                    calls++;
                    return new RedisCredentials("grasp-api", $"token-{calls}");
                },
            };

            var options = RedisProvider.BuildConfigurationOptions(config);
            Assert.That(options.Defaults, Is.Not.Null);
            // User + Password from one AUTH share a snapshot (one mint).
            Assert.That(options.User, Is.EqualTo("grasp-api"));
            Assert.That(options.Password, Is.EqualTo("token-1"));
            // Next AUTH remints.
            Assert.That(options.User, Is.EqualTo("grasp-api"));
            Assert.That(options.Password, Is.EqualTo("token-2"));
            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void Multiplexer_WithoutConnectionKey_UsesDatabaseMultiplexer()
        {
            var multiplexer = new Mock<IConnectionMultiplexer>();
            var database = new Mock<IDatabase>();
            database.SetupGet(d => d.Multiplexer).Returns(multiplexer.Object);
            var provider = new RedisProvider(database.Object);
            Assert.That(provider.Multiplexer, Is.SameAs(multiplexer.Object));
        }
    }
}
