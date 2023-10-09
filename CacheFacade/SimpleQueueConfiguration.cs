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
            int pollIntervalMillis = 1000)
        {
            this.QueueClient = queueClient;
            this.MessageProcessor = messageProcessor;
            this.MaxProcessingRate = maxProcessingRate;
            this.MaxBackgroundTasks = maxBackgroundTasks;
            this.BatchSize = batchSize;
            this.PollIntervalMillis = pollIntervalMillis;
            this.CancellationToken = cancellationToken;
        }

        public IQueueClient QueueClient { get; }

        public IMessageProcessor MessageProcessor { get; }

        public int MaxProcessingRate { get; }

        public int MaxBackgroundTasks { get; }

        public int BatchSize { get; }

        public int PollIntervalMillis { get; }

        public CancellationToken CancellationToken { get; }
    }
}
