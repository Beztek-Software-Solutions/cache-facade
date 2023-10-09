// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;

    internal class DisposableLock : IDisposable
    {
        private readonly ICache lockCache;
        private readonly string name;

        internal DisposableLock(ICache lockCache, string name)
        {
            this.lockCache = lockCache;
            this.name = name;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            System.GC.SuppressFinalize(this);
        }

        // Protected

        protected virtual void Dispose(bool disposing)
        {
            long[] lockData = this.lockCache.GetAsync<long[]>(this.name).Result;
            if (lockData != null)
            {
                if (lockData[2] > 1)
                {
                    lockData[2] = lockData[2] - 1;
                    this.lockCache.GetAndPutAsync<long[]>(this.name, lockData).Wait();
                }
                else
                {
                    this.lockCache.RemoveAsync<long[]>(this.name).Wait();
                }
            }
        }
    }
}
