﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Kafka.Clients
{
    using Furly.Extensions.Messaging;
    using AutoFixture;
    using FluentAssertions;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;
    using Xunit.Categories;

    [SystemTest]
    [Collection(KafkaCollection.Name)]
    public sealed class KafkaEventQueueTests : IDisposable
    {
        private readonly KafkaEventQueueHarness _harness;

        public KafkaEventQueueTests(KafkaServerFixture server, ITestOutputHelper output)
        {
            var fix = new Fixture();
            var topic = fix.Create<string>();
            _harness = new KafkaEventQueueHarness(server, topic, output);
        }

        public void Dispose()
        {
            _harness.Dispose();
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic1Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/+", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test3", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1", data, contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic2Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/#", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1/test3", data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync("test3", data, fix.Create<string>()).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1/test3", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic3Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/+/test", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1/test", data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync("test", data, fix.Create<string>()).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1/test", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventAndSubscribeWithTopic4Async()
        {
            var fix = new Fixture();

            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync("test/+/test/#", new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync("test1", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test2", data, fix.Create<string>()).ConfigureAwait(false);
            await eventClient.SendEventAsync("test/test1/test/testx/testy", data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync("test", data, fix.Create<string>()).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal("test/test1/test/testx/testy", result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventTest1Async()
        {
            var fix = new Fixture();
            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();
            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg => tcs.TrySetResult(arg))).ConfigureAwait(false);

            await eventClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventTest2Async()
        {
            var fix = new Fixture();
            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 5)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, data, contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target, fix.CreateMany<byte>().ToArray(), contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableFact]
        public async Task SendEventTestBatch1Async()
        {
            var fix = new Fixture();
            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == 16)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target,
                Enumerable.Range(1, 5).Select(_ => (ReadOnlyMemory<byte>)fix.CreateMany<byte>().ToArray()), contentType).ConfigureAwait(false);
            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, 10).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }

        [SkippableTheory]
        [InlineData(10)]
        // [InlineData(50)]
        // [InlineData(100)]
        // [InlineData(1000)]
        public async Task SendEventTestBatch2Async(int max)
        {
            var fix = new Fixture();
            var eventClient = _harness.GetEventClient();
            Skip.If(eventClient == null);

            var data = fix.CreateMany<byte>().ToArray();

            var contentType = fix.Create<string>();
            var target = fix.Create<string>();

            var count = 0;
            var tcs = new TaskCompletionSource<EventConsumerArg>(TaskCreationOptions.RunContinuationsAsynchronously);
            var eventSubscriber = _harness.GetEventSubscriber();
            Skip.If(eventSubscriber == null);
            await eventSubscriber.SubscribeAsync(target, new CallbackConsumer(arg =>
            {
                if (++count == max)
                {
                    tcs.TrySetResult(arg);
                }
            })).ConfigureAwait(false);

            var rand = new Random();
            await eventClient.SendEventAsync(target,
                Enumerable.Range(0, max).Select(_ => (ReadOnlyMemory<byte>)data), contentType).ConfigureAwait(false);

            var result = await tcs.Task.With2MinuteTimeout().ConfigureAwait(false);
            Assert.Equal(target, result.Target);
            Assert.Equal(contentType, result.ContentType);
            data.Should().BeEquivalentTo(result.Data);
        }
    }
}
