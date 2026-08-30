// Copyright (c) Beztek Software Solutions. All rights reserved.

namespace Beztek.Facade.Cache
{
    using System.Threading;
    using Beztek.Facade.Queue;

    /// <summary>
    /// Write-behind dequeue settings: queue client, message processor, and processing limits.
    /// Passed to <see cref="CacheConfiguration"/> when <see cref="CacheType.WriteBehind"/> is used.
    /// </summary>
    public class QueueConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="QueueConfiguration"/> class.
        /// </summary>
        /// <param name="queueClient">Queue facade client (local memory, Azure, or SQS).</param>
        /// <param name="messageProcessor">Typically a <see cref="CacheWriteBehindProcessor{T}"/> for <see cref="WriteBehindMessage"/>.</param>
        /// <param name="cancellationToken">Cancellation for the background dequeue loop.</param>
        /// <param name="maxProcessingRate">Max messages processed per second.</param>
        /// <param name="maxBackgroundTasks">Max concurrent background processing tasks.</param>
        /// <param name="batchSize">Messages claimed per dequeue batch.</param>
        /// <param name="pollIntervalMillis">Idle poll interval when the queue is empty.</param>
        /// <param name="maxProcessingAttempts">Failures before a message moves to the poison queue.</param>
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

        /// <summary>Queue client used to enqueue write-behind messages and run the drain loop.</summary>
        public IQueueClient QueueClient { get; }

        /// <summary>Processor invoked for each dequeued <see cref="WriteBehindMessage"/> (or batch).</summary>
        public IMessageProcessor MessageProcessor { get; }

        /// <summary>Maximum messages processed per second.</summary>
        public int MaxProcessingRate { get; }

        /// <summary>Maximum concurrent background processing tasks.</summary>
        public int MaxBackgroundTasks { get; }

        /// <summary>Messages claimed per dequeue batch.</summary>
        public int BatchSize { get; }

        /// <summary>Idle poll interval in milliseconds when the queue is empty.</summary>
        public int PollIntervalMillis { get; }

        /// <summary>Cancellation token for the background dequeue loop.</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// After this many receive attempts, still-failing write-behind messages move to the poison queue.
        /// Use <see cref="IQueueClient.PeekUnprocessedMessagesAsync"/> / <see cref="IQueueClient.RequeueUnprocessedMessagesAsync"/> to inspect or retry them.
        /// </summary>
        public int MaxProcessingAttempts { get; }
    }
}
