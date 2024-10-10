﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging.Runtime
{
    /// <summary>
    /// File system options
    /// </summary>
    public class FileSystemEventClientOptions
    {
        /// <summary>
        /// Output folder
        /// </summary>
        public string? OutputFolder { get; set; }

        /// <summary>
        /// Max message size
        /// </summary>
        public int? MessageMaxBytes { get; set; }
    }
}
