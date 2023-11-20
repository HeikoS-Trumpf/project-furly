﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly.Extensions.Messaging
{
    using System;

    /// <summary>
    /// Register a listener
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEventRegistration<T> where T : class
    {
        /// <summary>
        /// Register listener
        /// </summary>
        /// <param name="listener"></param>
        IDisposable Register(T listener);
    }
}
