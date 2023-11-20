// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using Furly.Extensions.Kafka;
    using Furly.Extensions.Hosting;
    using Furly.Extensions.Messaging;
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Kafka producer
    /// </summary>
    public sealed class KafkaProducerClient : IEventClient, IDisposable
    {
        /// <inheritdoc/>
        public string Name => "Kafka";

        /// <inheritdoc/>
        public int MaxEventPayloadSizeInBytes { get; }

        /// <inheritdoc/>
        public string Identity => _producer.Name;

        /// <summary>
        /// Create service client
        /// </summary>
        /// <param name="admin"></param>
        /// <param name="server"></param>
        /// <param name="config"></param>
        /// <param name="logger"></param>
        /// <param name="identity"></param>
        public KafkaProducerClient(IKafkaAdminClient admin,
            IOptionsSnapshot<KafkaServerOptions> server,
            IOptionsSnapshot<KafkaProducerOptions> config,
            ILogger<KafkaProducerClient> logger, IProcessIdentity? identity = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _producer = new ProducerBuilder<string, byte[]>(
                server.Value.ToClientConfig<ProducerConfig>(
                    identity?.Id ?? Dns.GetHostName()))
                .SetErrorHandler(OnError)
                .SetStatisticsHandler(OnMetrics)
                .SetLogHandler((_, m) => _logger.Log(m))
                .Build();
            MaxEventPayloadSizeInBytes = config.Value.MessageMaxBytes ?? 1024 * 1024;
            _topic = EnsureTopicAsync(admin, config.Value.Topic);
        }

        /// <inheritdoc/>
        public IEvent CreateEvent()
        {
            return new KafkaEvent(this);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _producer?.Dispose();
            _topics.Clear();
        }

        /// <summary>
        /// Handle error
        /// </summary>
        /// <param name="client"></param>
        /// <param name="error"></param>
        private void OnError(IProducer<string, byte[]> client, Error error)
        {
            // Todo
        }

        /// <summary>
        /// Handle metrics
        /// </summary>
        /// <param name="client"></param>
        /// <param name="json"></param>
        private void OnMetrics(IProducer<string, byte[]> client, string json)
        {
        }

        /// <summary>
        /// Helper to create topic
        /// </summary>
        /// <param name="admin"></param>
        /// <param name="topic"></param>
        /// <returns></returns>
        private static async Task<string> EnsureTopicAsync(IKafkaAdminClient admin,
            string? topic)
        {
            if (topic == null)
            {
                return string.Empty;
            }
            await admin.EnsureTopicExistsAsync(topic).ConfigureAwait(false);
            return topic;
        }

        /// <summary>
        /// Event wrapper
        /// </summary>
        private sealed class KafkaEvent : IEvent
        {
            /// <summary>
            /// Create event
            /// </summary>
            /// <param name="outer"></param>
            public KafkaEvent(KafkaProducerClient outer)
            {
                _outer = outer;
            }

            /// <inheritdoc/>
            public IEvent SetQoS(QoS value)
            {
                _qos = value;
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTopic(string? value)
            {
                if (value != null)
                {
                    _topic = value;
                    _header.Add("Topic", Encoding.UTF8.GetBytes(value));
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTimestamp(DateTime value)
            {
                _timestamp = new Timestamp(value);
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentType(string? value)
            {
                if (value != null)
                {
                    _header.Add("ContentType", Encoding.UTF8.GetBytes(value));
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetContentEncoding(string? value)
            {
                if (value != null)
                {
                    _header.Add("ContentEncoding", Encoding.UTF8.GetBytes(value));
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddProperty(string name, string? value)
            {
                if (value != null)
                {
                    _header.Add(name, Encoding.UTF8.GetBytes(value));
                }
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetRetain(bool value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent SetTtl(TimeSpan value)
            {
                return this;
            }

            /// <inheritdoc/>
            public IEvent AddBuffers(IEnumerable<ReadOnlyMemory<byte>> value)
            {
                _buffers.AddRange(value);
                return this;
            }

            /// <inheritdoc/>
            public async ValueTask SendAsync(CancellationToken ct)
            {
                if (_buffers.Count == 0)
                {
                    return;
                }
                if (_topic == null)
                {
                    throw new InvalidOperationException("Need topic");
                }
                var topicHandle = await _outer._topic.ConfigureAwait(false);
                foreach (var payload in _buffers)
                {
                    var ev = new Message<string, byte[]>
                    {
                        Key = _topic,
                        Timestamp = _timestamp,
                        Value = payload.ToArray(),
                        Headers = _header
                    };
                    var result = await _outer._producer.ProduceAsync(topicHandle, ev, ct).ConfigureAwait(false);
                    _outer._logger.LogTrace(
                        "Written with {Status} to {Topic} (Part:{Partition} Offset:{Offset})",
                        result.Status, result.Topic, result.TopicPartition.Partition.Value,
                        result.TopicPartitionOffset.Offset.Value);
                }
                if (_qos == QoS.AtMostOnce)
                {
                    return;
                }
                // TODO: Use delivery result to await tcs
                await Task.Run(() => _outer._producer.Flush(ct), ct).ConfigureAwait(false);
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                _buffers.Clear();
            }

            private string _topic = string.Empty;
            private QoS _qos = QoS.AtLeastOnce;
            private Timestamp _timestamp;
            private readonly Headers _header = new();
            private readonly List<ReadOnlyMemory<byte>> _buffers = new();
            private readonly KafkaProducerClient _outer;
        }

        private readonly ConcurrentDictionary<string, Task<string>> _topics = new();
        private readonly IProducer<string, byte[]> _producer;
        private readonly Task<string> _topic;
        private readonly ILogger _logger;
    }
}
