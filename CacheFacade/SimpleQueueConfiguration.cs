// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Threading;
    using Beztek.Facade.Queue;

    public class QueueConfiguration
    {
        public QueueConfiguration(IQueueClient queueClient,
            IMessageProcessor messageProcessor,
            CancellationToken cancellationToken,
            int maxProcessingRate = 1000,
            int maxBackgroundTasks = 200,
            int batchSize = 100,
            int pollIntervalMillis = 1000,
            int maxProcessingAttempts = QueueDequeueConfig.DefaultMaxProcessingAttempts)
        {
            this.QueueClient = queueClient;
            this.MessageProcessor = messageProcessor;
            this.MaxProcessingRate = maxProcessingRate;
            this.MaxBackgroundTasks = maxBackgroundTasks;
            this.BatchSize = batchSize;
            this.PollIntervalMillis = pollIntervalMillis;
            this.CancellationToken = cancellationToken;
            this.MaxProcessingAttempts = maxProcessingAttempts;
        }

        public IQueueClient QueueClient { get; }

        public IMessageProcessor MessageProcessor { get; }

        public int MaxProcessingRate { get; }

        public int MaxBackgroundTasks { get; }

        public int BatchSize { get; }

        public int PollIntervalMillis { get; }

        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// After this many receive attempts, still-failing write-behind messages move to the poison queue.
        /// Use <see cref="IQueueClient.PeekUnprocessedMessagesAsync"/> / <see cref="IQueueClient.RequeueUnprocessedMessagesAsync"/> to inspect or retry them.
        /// </summary>
        public int MaxProcessingAttempts { get; }
    }
}
