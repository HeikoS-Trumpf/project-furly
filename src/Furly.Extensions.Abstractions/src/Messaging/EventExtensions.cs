﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Event extensions
    /// </summary>
    public static class EventExtensions
    {
        /// <summary>
        /// Send event to a target resource. The data buffers must not be
        /// larger than <see cref="IEventClient.MaxEventPayloadSizeInBytes"/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="topic"></param>
        /// <param name="buffers"></param>
        /// <param name="contentType"></param>
        /// <param name="contentEncoding"></param>
        /// <param name="configure"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static async Task SendEventAsync(this IEventClient client,
            string topic, IEnumerable<ReadOnlyMemory<byte>> buffers,
            string contentType, string? contentEncoding = null,
            Action<IEvent>? configure = null, CancellationToken ct = default)
        {
            using var msg = client.CreateEvent();
            var sending = msg
                .SetTopic(topic)
                .AddBuffers(buffers)
                .SetContentType(contentType)
                .SetContentEncoding(contentEncoding);
            configure?.Invoke(sending);
            await sending.SendAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Send message to a target resource. The data buffer must not be
        /// larger than <see cref="IEventClient.MaxEventPayloadSizeInBytes"/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="topic"></param>
        /// <param name="buffer"></param>
        /// <param name="contentType"></param>
        /// <param name="contentEncoding"></param>
        /// <param name="configure"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public static Task SendEventAsync(this IEventClient client,
            string topic, ReadOnlyMemory<byte> buffer, string contentType,
            string? contentEncoding = null, Action<IEvent>? configure = null,
            CancellationToken ct = default)
        {
            return client.SendEventAsync(topic,
                new[] { buffer }, contentType, contentEncoding, configure, ct);
        }
    }
}
