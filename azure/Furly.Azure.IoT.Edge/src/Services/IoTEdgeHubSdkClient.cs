// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.IoT.Edge.Services
{
    using Furly.Azure.IoT.Edge;
    using Furly.Exceptions;
    using Furly.Extensions.Utils;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Injectable IoT Sdk client
    /// </summary>
    public sealed class IoTEdgeHubSdkClient : IIoTEdgeDeviceClient, IDisposable
    {
        /// <summary>
        /// Create sdk factory
        /// </summary>
        /// <param name="options"></param>
        /// <param name="identity"></param>
        /// <param name="logger"></param>
        /// <param name="callback"></param>
        public IoTEdgeHubSdkClient(IOptions<IoTEdgeClientOptions> options,
            IIoTEdgeDeviceIdentity identity, ILogger<IoTEdgeHubSdkClient> logger,
            IIoTEdgeClientState? callback = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _callback = callback;

            TransportOption transportToUse;
            if (!string.IsNullOrEmpty(identity.Gateway))
            {
                //
                // Running in edge mode
                //
                // We force the configured transport (if provided) to it's OverTcp
                // variant as follows: AmqpOverTcp when Amqp, AmqpOverWebsocket or
                // AmqpOverTcp specified and MqttOverTcp otherwise.
                // Default is MqttOverTcp
                //
                if ((_options.Value.Transport & TransportOption.Mqtt) != 0)
                {
                    // prefer Mqtt over Amqp due to performance reasons
                    transportToUse = TransportOption.MqttOverTcp;
                }
                else
                {
                    transportToUse = TransportOption.AmqpOverTcp;
                }
                _logger.LogDebug("Connecting all clients to {EdgeHub} using {Transport}.",
                    identity.Gateway, transportToUse);
            }
            else
            {
                transportToUse = _options.Value.Transport == 0 ?
                    TransportOption.Any : _options.Value.Transport;
            }

            var cs = string.IsNullOrEmpty(options.Value.EdgeHubConnectionString) ? null :
                IotHubConnectionStringBuilder.Create(options.Value.EdgeHubConnectionString);

            var caFile = Environment.GetEnvironmentVariable("EdgeModuleCACertificateFile");
            if (caFile != null && File.Exists(caFile) &&
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" &&
                Environment.GetEnvironmentVariable(
                    nameof(options.Value.EdgeHubConnectionString)) != null)
            {
                _logger.LogInformation("---------- Running in iotedgehubdev mode ---------");
                cs = null; // This forces the use of sdk reading from environment variables.
            }

            var transportSettings = GetTransportSettings(transportToUse);
            _client = CreateAdapterAsync(cs, transportSettings);
        }

        /// <inheritdoc/>
        public async Task SendEventAsync(Message message, string? output,
            CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            await client.SendEventAsync(message, output, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SendEventBatchAsync(IEnumerable<Message> messages,
            string? output, CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            await client.SendEventBatchAsync(messages, output, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetMethodHandlerAsync(MethodCallback? methodHandler,
            object? userContext, CancellationToken ct = default)
        {
            var client = await _client.ConfigureAwait(false);
            await client.SetMethodHandlerAsync(methodHandler,
                userContext, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task SetMessageHandlerAsync(MessageHandler? messageHandler,
            object? userContext, CancellationToken ct = default)
        {
            var client = await _client.ConfigureAwait(false);
            await client.SetMessageHandlerAsync(messageHandler,
                userContext, ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback callback,
            object? userContext, CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            await client.SetDesiredPropertyUpdateCallbackAsync(callback,
                userContext, ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<Twin> GetTwinAsync(CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            return await client.GetTwinAsync(ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties,
            CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            await client.UpdateReportedPropertiesAsync(reportedProperties,
                ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MethodResponse> InvokeMethodAsync(
            string deviceId, string moduleId, MethodRequest methodRequest,
            CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            return await client.InvokeMethodAsync(deviceId, moduleId,
                methodRequest, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<MethodResponse> InvokeMethodAsync(
            string deviceId, MethodRequest methodRequest, CancellationToken ct)
        {
            var client = await _client.ConfigureAwait(false);
            return await client.InvokeMethodAsync(deviceId,
                methodRequest, ct).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task CloseAsync()
        {
            var client = await _client.ConfigureAwait(false);
            if (client != null)
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            var client = Try.Op(() => _client?.Result);
            if (client != null)
            {
                try
                {
                    client.CloseAsync().GetAwaiter().GetResult();
                }
                catch { }
                finally
                {
                    (client as IDisposable)?.Dispose();
                }
            }
        }

        /// <summary>
        /// Get transport settings list for transport
        /// </summary>
        /// <param name="transport"></param>
        /// <returns></returns>
        private static List<ITransportSettings> GetTransportSettings(
            TransportOption transport)
        {
            // Configure transport settings
            var transportSettings = new List<ITransportSettings>();
            if ((transport & TransportOption.MqttOverTcp) != 0)
            {
                var setting = new MqttTransportSettings(
                    TransportType.Mqtt_Tcp_Only);
                transportSettings.Add(setting);
            }
            if ((transport & TransportOption.MqttOverWebsocket) != 0)
            {
                var setting = new MqttTransportSettings(
                    TransportType.Mqtt_WebSocket_Only);
                transportSettings.Add(setting);
            }
            if ((transport & TransportOption.AmqpOverTcp) != 0)
            {
                var setting = new AmqpTransportSettings(
                    TransportType.Amqp_Tcp_Only);
                transportSettings.Add(setting);
            }
            if ((transport & TransportOption.AmqpOverWebsocket) != 0)
            {
                var setting = new AmqpTransportSettings(
                    TransportType.Amqp_WebSocket_Only);
                transportSettings.Add(setting);
            }
            return transportSettings;
        }

        /// <summary>
        /// Create client adapter
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        private async Task<IIoTEdgeDeviceClient> CreateAdapterAsync(
            IotHubConnectionStringBuilder? cs, List<ITransportSettings> settings)
        {
            if (settings.Count != 0)
            {
                var exceptions = new List<Exception>();
                foreach (var option in settings
                    .Select<ITransportSettings, Func<Task<IIoTEdgeDeviceClient>>>(t =>
                         () => CreateAdapterAsync(cs, t)))
                {
                    try
                    {
                        return await option().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                }
                throw new AggregateException(exceptions);
            }
            return await CreateAdapterAsync(cs,
                (ITransportSettings?)null).ConfigureAwait(false);
        }

        /// <summary>
        /// Create client adapter
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="setting"></param>
        /// <returns></returns>
        private Task<IIoTEdgeDeviceClient> CreateAdapterAsync(
            IotHubConnectionStringBuilder? cs, ITransportSettings? setting)
        {
            var timeout = TimeSpan.FromMinutes(5);
            var product = _options.Value.Product;
            if (string.IsNullOrEmpty(product))
            {
                product = "iiot";
            }
            if (string.IsNullOrEmpty(_identity.ModuleId))
            {
                return cs == null
                    ? throw new InvalidConfigurationException(
                        "No connection string for device client specified.")
                    : DeviceClientAdapter.CreateAsync(product, cs,
                    _identity.DeviceId, setting, timeout, _logger, _callback);
            }
            return ModuleClientAdapter.CreateAsync(product, cs, _identity.DeviceId,
                _identity.ModuleId, setting, timeout, _logger, _callback);
        }

        /// <summary>
        /// Adapts module client to interface
        /// </summary>
        internal sealed class ModuleClientAdapter : IIoTEdgeDeviceClient
        {
            /// <summary>
            /// Whether the client is closed
            /// </summary>
            public bool IsClosed { get; internal set; }

            /// <summary>
            /// Create client
            /// </summary>
            /// <param name="client"></param>
            private ModuleClientAdapter(ModuleClient client)
            {
                _client = client ?? throw new ArgumentNullException(nameof(client));
            }

            /// <summary>
            /// Factory
            /// </summary>
            /// <param name="product"></param>
            /// <param name="cs"></param>
            /// <param name="deviceId"></param>
            /// <param name="moduleId"></param>
            /// <param name="transportSetting"></param>
            /// <param name="timeout"></param>
            /// <param name="logger"></param>
            /// <param name="callback"></param>
            /// <returns></returns>
            public static async Task<IIoTEdgeDeviceClient> CreateAsync(string product,
                IotHubConnectionStringBuilder? cs, string deviceId, string moduleId,
                ITransportSettings? transportSetting, TimeSpan timeout,
                ILogger logger, IIoTEdgeClientState? callback)
            {
                if (cs == null)
                {
                    logger.LogInformation("Running in iotedge context.");
                }
                else
                {
                    logger.LogInformation("Running outside iotedge context.");
                }

                var client = await CreateAsync(cs, transportSetting).ConfigureAwait(false);
                var adapter = new ModuleClientAdapter(client);
                callback ??= new ConnectionLogger(logger);
                try
                {
                    // Configure
                    client.OperationTimeoutInMilliseconds = (uint)timeout.TotalMilliseconds;
                    client.SetConnectionStatusChangesHandler((s, r) =>
                        adapter.OnConnectionStatusChange(callback, deviceId, moduleId, s, r));
                    client.ProductInfo = product;
                    await client.OpenAsync().ConfigureAwait(false);
                    callback.OnOpened(deviceId, moduleId);
                    return adapter;
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task CloseAsync()
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    _client.OperationTimeoutInMilliseconds = 3000;
                    _client.SetRetryPolicy(new NoRetry());
                    IsClosed = true;
                    await _client.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SendEventAsync(Message message, string? output,
                CancellationToken ct)
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        await _client.SendEventAsync(output, message,
                            ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _client.SendEventAsync(message, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SendEventBatchAsync(IEnumerable<Message> messages,
                 string? output, CancellationToken ct)
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    if (!string.IsNullOrEmpty(output))
                    {
                        await _client.SendEventBatchAsync(output,
                            messages, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _client.SendEventBatchAsync(messages, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SetMethodHandlerAsync(MethodCallback? methodHandler,
                object? userContext, CancellationToken ct)
            {
                try
                {
                    await _client.SetMethodDefaultHandlerAsync(methodHandler,
                        userContext, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SetMessageHandlerAsync(MessageHandler? messageHandler,
                object? userContext, CancellationToken ct)
            {
                try
                {
                    await _client.SetMessageHandlerAsync(messageHandler,
                        userContext, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback callback,
                object? userContext, CancellationToken ct)
            {
                try
                {
                    await _client.SetDesiredPropertyUpdateCallbackAsync(callback, userContext, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task<Twin> GetTwinAsync(CancellationToken ct)
            {
                try
                {
                    return await _client.GetTwinAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties,
                CancellationToken ct)
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    await _client.UpdateReportedPropertiesAsync(reportedProperties, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task<MethodResponse> InvokeMethodAsync(string deviceId, string? moduleId,
                MethodRequest methodRequest, CancellationToken ct)
            {
                try
                {
                    return await _client.InvokeMethodAsync(deviceId,
                        moduleId, methodRequest, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task<MethodResponse> InvokeMethodAsync(string deviceId,
                MethodRequest methodRequest, CancellationToken ct)
            {
                try
                {
                    return await _client.InvokeMethodAsync(deviceId,
                        methodRequest, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <summary>
            /// Handle status change event
            /// </summary>
            /// <param name="callback"></param>
            /// <param name="deviceId"></param>
            /// <param name="moduleId"></param>
            /// <param name="status"></param>
            /// <param name="reason"></param>
            private void OnConnectionStatusChange(IIoTEdgeClientState callback, string deviceId,
                string moduleId, ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                if (status == ConnectionStatus.Connected)
                {
                    callback.OnConnected(_reconnectCounter, deviceId, moduleId, reason.ToString());
                    _reconnectCounter++;
                    return;
                }
                if (IsClosed)
                {
                    // Already closed - nothing to do
                    return;
                }
                if (status == ConnectionStatus.Disconnected ||
                    status == ConnectionStatus.Disabled)
                {
                    // Force
                    callback.OnClosed(_reconnectCounter, deviceId, moduleId, reason.ToString());
                    IsClosed = true;
                    return;
                }
                callback.OnDisconnected(_reconnectCounter, deviceId, moduleId, reason.ToString());
            }

            /// <summary>
            /// Helper to create module client
            /// </summary>
            /// <param name="cs"></param>
            /// <param name="transportSetting"></param>
            /// <returns></returns>
            private static async Task<ModuleClient> CreateAsync(IotHubConnectionStringBuilder? cs,
                ITransportSettings? transportSetting)
            {
                try
                {
                    if (transportSetting == null)
                    {
                        return cs == null
                            ? await ModuleClient.CreateFromEnvironmentAsync().ConfigureAwait(false)
#pragma warning disable CA2000 // Dispose objects before losing scope
                            : ModuleClient.CreateFromConnectionString(cs.ToString());
#pragma warning restore CA2000 // Dispose objects before losing scope
                    }
                    var ts = new ITransportSettings[] { transportSetting };
                    return cs == null
                        ? await ModuleClient.CreateFromEnvironmentAsync(ts).ConfigureAwait(false)
#pragma warning disable CA2000 // Dispose objects before losing scope
                        : ModuleClient.CreateFromConnectionString(cs.ToString(), ts);
#pragma warning restore CA2000 // Dispose objects before losing scope
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            private sealed class ConnectionLogger : IIoTEdgeClientState
            {
                public ConnectionLogger(ILogger logger)
                {
                    _logger = logger;
                }

                /// <inheritdoc/>
                public void OnClosed(int counter, string deviceId, string? moduleId, string reason)
                {
                    _logger.LogInformation("{Counter}: Module {DeviceId}_{ModuleId} closed " +
                        "due to {Reason}.", counter, deviceId, moduleId, reason);
                }

                /// <inheritdoc/>
                public void OnConnected(int counter, string deviceId, string? moduleId, string reason)
                {
                    _logger.LogInformation("{Counter}: Module {DeviceId}_{ModuleId} reconnected " +
                        "due to {Reason}.", counter, deviceId, moduleId, reason);
                }

                /// <inheritdoc/>
                public void OnDisconnected(int counter, string deviceId, string? moduleId, string reason)
                {
                    _logger.LogInformation("{Counter}: Module {DeviceId}_{ModuleId} disconnected " +
                        "due to {Reason}...", counter, deviceId, moduleId, reason);
                }

                /// <inheritdoc/>
                public void OnOpened(string deviceId, string? moduleId)
                {
                    _logger.LogInformation("0: Module {DeviceId}_{ModuleId} opened.",
                        deviceId, moduleId);
                }

                private readonly ILogger _logger;
            }

            private readonly ModuleClient _client;
            private int _reconnectCounter;
        }

        /// <summary>
        /// Adapts device client to interface
        /// </summary>
        private sealed class DeviceClientAdapter : IIoTEdgeDeviceClient
        {
            /// <summary>
            /// Whether the client is closed
            /// </summary>
            public bool IsClosed { get; internal set; }

            /// <summary>
            /// Create client
            /// </summary>
            /// <param name="client"></param>
            internal DeviceClientAdapter(DeviceClient client)
            {
                _client = client ?? throw new ArgumentNullException(nameof(client));
            }

            /// <summary>
            /// Factory
            /// </summary>
            /// <param name="product"></param>
            /// <param name="cs"></param>
            /// <param name="deviceId"></param>
            /// <param name="transportSetting"></param>
            /// <param name="timeout"></param>
            /// <param name="logger"></param>
            /// <param name="callback"></param>
            /// <returns></returns>
            public static async Task<IIoTEdgeDeviceClient> CreateAsync(string product,
                IotHubConnectionStringBuilder? cs, string deviceId,
                ITransportSettings? transportSetting, TimeSpan timeout, ILogger logger,
                IIoTEdgeClientState? callback)
            {
                var client = Create(cs, transportSetting);
                var adapter = new DeviceClientAdapter(client);
                callback ??= new ConnectionLogger(logger);
                try
                {
                    // Configure
                    client.OperationTimeoutInMilliseconds = (uint)timeout.TotalMilliseconds;
                    client.SetConnectionStatusChangesHandler((s, r) =>
                        adapter.OnConnectionStatusChange(callback, deviceId, s, r));
                    client.ProductInfo = product;

                    await client.OpenAsync().ConfigureAwait(false);
                    callback.OnOpened(deviceId, null);
                    return adapter;
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task CloseAsync()
            {
                if (IsClosed)
                {
                    return;
                }
                _client.OperationTimeoutInMilliseconds = 3000;
                _client.SetRetryPolicy(new NoRetry());
                IsClosed = true;
                await _client.CloseAsync().ConfigureAwait(false);
            }

            /// <inheritdoc />
            public async Task SendEventAsync(Message message, string? outputName,
                CancellationToken ct)
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    await _client.SendEventAsync(message, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SendEventBatchAsync(IEnumerable<Message> messages,
                string? outputName, CancellationToken ct)
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    await _client.SendEventBatchAsync(messages, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SetMethodHandlerAsync(MethodCallback? methodHandler,
                object? userContext, CancellationToken ct)
            {
                try
                {
                    await _client.SetMethodDefaultHandlerAsync(methodHandler,
                        userContext, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback callback,
                object? userContext, CancellationToken ct)
            {
                try
                {
                    await _client.SetDesiredPropertyUpdateCallbackAsync(callback, userContext, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task<Twin> GetTwinAsync(CancellationToken ct)
            {
                try
                {
                    return await _client.GetTwinAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties,
                CancellationToken ct)
            {
                if (IsClosed)
                {
                    return;
                }
                try
                {
                    await _client.UpdateReportedPropertiesAsync(reportedProperties, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public async Task SetMessageHandlerAsync(MessageHandler? messageHandler,
                object? userContext, CancellationToken ct)
            {
                try
                {
                    if (messageHandler == null)
                    {
                        await _client.SetReceiveMessageHandlerAsync(null, userContext,
                            ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await _client.SetReceiveMessageHandlerAsync(async (message, ctx) =>
                        {
                            var dispo = await messageHandler.Invoke(message, ctx).ConfigureAwait(false);
                            try
                            {
                                switch (dispo)
                                {
                                    case MessageResponse.Completed:
                                        await _client.CompleteAsync(message).ConfigureAwait(false);
                                        break;
                                    case MessageResponse.Abandoned:
                                        await _client.AbandonAsync(message).ConfigureAwait(false);
                                        break;
                                    default:
                                        await _client.RejectAsync(message).ConfigureAwait(false);
                                        break;
                                }
                            }
                            catch (NotSupportedException)
                            {
                                // Sdk does not support ack on the chosen transport
                            }
                        }, userContext, ct).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            /// <inheritdoc />
            public Task<MethodResponse> InvokeMethodAsync(string deviceId, string moduleId,
                MethodRequest methodRequest, CancellationToken ct)
            {
                return Task.FromException<MethodResponse>(
                    new NotSupportedException("Device client does not support methods"));
            }

            /// <inheritdoc />
            public Task<MethodResponse> InvokeMethodAsync(string deviceId,
                MethodRequest methodRequest, CancellationToken ct)
            {
                return Task.FromException<MethodResponse>(
                    new NotSupportedException("Device client does not support methods"));
            }

            /// <summary>
            /// Handle status change event
            /// </summary>
            /// <param name="callback"></param>
            /// <param name="deviceId"></param>
            /// <param name="status"></param>
            /// <param name="reason"></param>
            private void OnConnectionStatusChange(IIoTEdgeClientState callback, string deviceId,
                ConnectionStatus status, ConnectionStatusChangeReason reason)
            {
                if (status == ConnectionStatus.Connected)
                {
                    callback.OnConnected(_reconnectCounter, deviceId, null, reason.ToString());
                    _reconnectCounter++;
                    return;
                }
                if (IsClosed)
                {
                    // Already closed - nothing to do
                    return;
                }
                if (status == ConnectionStatus.Disconnected ||
                    status == ConnectionStatus.Disabled)
                {
                    // Force
                    IsClosed = true;
                    callback.OnClosed(_reconnectCounter, deviceId, null, reason.ToString());
                    return;
                }
                callback.OnDisconnected(_reconnectCounter, deviceId, null, reason.ToString());
            }

            /// <summary>
            /// Helper to create device client
            /// </summary>
            /// <param name="cs"></param>
            /// <param name="transportSetting"></param>
            /// <returns></returns>
            private static DeviceClient Create(IotHubConnectionStringBuilder? cs,
                ITransportSettings? transportSetting)
            {
                try
                {
                    ArgumentNullException.ThrowIfNull(cs);
                    return transportSetting != null
                        ? DeviceClient.CreateFromConnectionString(cs.ToString(),
                            [transportSetting])
                        : DeviceClient.CreateFromConnectionString(cs.ToString());
                }
                catch (Exception ex)
                {
                    throw Translate(ex);
                }
            }

            private sealed class ConnectionLogger : IIoTEdgeClientState
            {
                public ConnectionLogger(ILogger logger)
                {
                    _logger = logger;
                }

                /// <inheritdoc/>
                public void OnClosed(int counter, string deviceId, string? moduleId, string reason)
                {
                    _logger.LogInformation("{Counter}: Device {DeviceId} closed due to {Reason}.",
                        counter, deviceId, reason);
                }

                /// <inheritdoc/>
                public void OnConnected(int counter, string deviceId, string? moduleId, string reason)
                {
                    _logger.LogInformation("{Counter}: Device {DeviceId} reconnected due to {Reason}.",
                        counter, deviceId, reason);
                }

                /// <inheritdoc/>
                public void OnDisconnected(int counter, string deviceId, string? moduleId, string reason)
                {
                    _logger.LogInformation("{Counter}: Device {DeviceId} disconnected due to {Reason}...",
                        counter, deviceId, reason);
                }

                /// <inheritdoc/>
                public void OnOpened(string deviceId, string? moduleId)
                {
                    _logger.LogInformation("0: Module {DeviceId} opened.", deviceId);
                }

                private readonly ILogger _logger;
            }

            private readonly DeviceClient _client;
            private int _reconnectCounter;
        }

        /// <summary>
        /// IoT hub Message system property names
        /// </summary>
        internal static class SystemProperties
        {
            /// <summary>
            /// Target
            /// </summary>
            public const string To = "to";
        }

        /// <summary>
        /// Translate exception
        /// </summary>
        internal static Exception Translate(Exception ex)
        {
            switch (ex)
            {
                case DeviceNotFoundException dnf:
                    return new ResourceNotFoundException(dnf.Message, dnf);
                case DeviceMaximumQueueDepthExceededException dm:
                    return new ResourceExhaustionException(dm.Message, dm);
                case QuotaExceededException qee:
                    return new ResourceExhaustionException(qee.Message, qee);
                case DeviceMessageLockLostException dle:
                    return new ResourceInvalidStateException(dle.Message, dle);
                case MessageTooLargeException mtl:
                    return new MessageSizeLimitException(mtl.Message, mtl);
                case ServerBusyException sb:
                    return new TemporarilyBusyException(sb.Message, sb);
                case IotHubThrottledException te:
                    return new TemporarilyBusyException(te.Message, te);
            }
            return ex;
        }

        private readonly Task<IIoTEdgeDeviceClient> _client;
        private readonly ILogger _logger;
        private readonly IIoTEdgeClientState? _callback;
        private readonly IOptions<IoTEdgeClientOptions> _options;
        private readonly IIoTEdgeDeviceIdentity _identity;
    }
}
