// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache.Tests.Live
{
    using NUnit.Framework;

    [TestFixture]
    public class LiveContainerRuntimeTests
    {
        [Test]
        public void EnsureConfigured_IsIdempotent()
        {
            LiveContainerRuntime.EnsureConfigured();
            LiveContainerRuntime.EnsureConfigured();
        }

        [Test]
        public void FindPodmanSocket_DoesNotThrow()
        {
            string socket = LiveContainerRuntime.FindPodmanSocket();
            Assert.Pass(socket == null ? "no podman socket" : socket);
        }
    }
}
