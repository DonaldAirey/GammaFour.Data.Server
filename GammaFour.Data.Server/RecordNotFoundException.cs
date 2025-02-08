// <copyright file="RecordNotFoundException.cs" company="Donald Roy Airey">
//    Copyright © 2025 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents errors that occur calling the unmanaged Win32 libraries.
    /// </summary>
    /// <param name="table">The table where the exception occurred.</param>
    /// <param name="key">The key that caused the exception.</param>
    public class RecordNotFoundException(string table, object[] key)
        : Exception(Resource.RecordNotFoundError)
    {
        /// <summary>
        /// Gets the table where the exception occurred.
        /// </summary>
        public string Table { get; } = table;

        /// <summary>
        /// Gets the key that caused the exception.
        /// </summary>
        public ReadOnlyCollection<object> Key { get; } = new ReadOnlyCollection<object>(key);
    }
}