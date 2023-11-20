﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Tunnel.Azure.IoT.Service
{
    using Furly.Tunnel.Services;
    using Microsoft.Extensions.Hosting;
    using System.Threading;
    using System.Threading.Tasks;

    /// <inheritdoc/>
    public sealed class CloudProxy : IHostedService
    {
        /// <summary>
        /// Create host service
        /// </summary>
        /// <param name="server"></param>
        public CloudProxy(HttpTunnelHybridServer server)
        {
            _server = server;
        }

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _server;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private readonly HttpTunnelHybridServer _server;
    }
}
