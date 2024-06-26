﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Azure.EventHubs
{
    /// <summary>
    /// Configuration for service
    /// </summary>
    public class EventHubsClientOptions
    {
        /// <summary>
        /// Connection string
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Max payload size. Set to 256k for basic tier.
        /// Default is 1MB
        /// </summary>
        public int? MaxEventPayloadSizeInBytes { get; set; }

        /// <summary>
        /// Schema registry, set to null to disable publishing
        /// schemas
        /// </summary>
        public SchemaRegistryOptions? SchemaRegistry { get; set; }
    }
}
