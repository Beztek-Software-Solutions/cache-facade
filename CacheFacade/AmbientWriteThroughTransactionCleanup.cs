// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Transactions;

    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Tracks WriteThrough provider keys mutated under an ambient
    /// <see cref="TransactionScope"/> and evicts them if the transaction aborts.
    /// Subscribes to <see cref="Transaction.TransactionCompleted"/> (does not
    /// <c>EnlistVolatile</c>) so SQL drivers are not forced into two-phase prepare.
    /// </summary>
    /// <remarks>
    /// Write-behind is not supported: queued persistence is outside the ambient SQL transaction.
    /// </remarks>
    internal sealed class AmbientWriteThroughTransactionCleanup
    {
        private readonly Cache cache;
        private readonly string transactionLocalId;
        private readonly HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        private readonly object gate = new object();
        private int finished;

        public AmbientWriteThroughTransactionCleanup(Cache cache, string transactionLocalId)
        {
            this.cache = cache;
            this.transactionLocalId = transactionLocalId;
        }

        public void Track(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            lock (this.gate)
            {
                this.keys.Add(key);
            }
        }

        public void OnTransactionCompleted(object sender, TransactionEventArgs e)
        {
            try
            {
                Transaction transaction = e?.Transaction;
                if (transaction != null
                    && transaction.TransactionInformation.Status == TransactionStatus.Aborted)
                {
                    this.EvictTrackedKeys();
                }
            }
            finally
            {
                this.Finish();
            }
        }

        private void EvictTrackedKeys()
        {
            string[] snapshot;
            lock (this.gate)
            {
                snapshot = new string[this.keys.Count];
                this.keys.CopyTo(snapshot);
                this.keys.Clear();
            }

            foreach (string key in snapshot)
            {
                try
                {
                    this.cache.EvictProviderKey(key);
                }
                catch (Exception ex)
                {
                    this.cache.FacadeLogger?.LogWarning(
                        ex,
                        "Ambient transaction abort: failed to evict WriteThrough cache key {Key}",
                        key);
                }
            }
        }

        private void Finish()
        {
            if (Interlocked.Exchange(ref this.finished, 1) == 0)
            {
                this.cache.ClearAmbientWriteThroughCleanup(this.transactionLocalId);
            }
        }
    }
}
