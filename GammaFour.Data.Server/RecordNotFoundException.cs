// <copyright file="RecordNotFoundException.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Represents errors that occur calling the unmanaged Win32 libraries.
    /// </summary>
    public class RecordNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecordNotFoundException"/> class.
        /// </summary>
        /// <param name="table">The table where the exception occurred.</param>
        /// <param name="key">The key that caused the exception.</param>
        public RecordNotFoundException(string table, object[] key)
            : base(Resource.RecordNotFoundError)
        {
            // Initialize the object.
            this.Table = table;
            this.Key = new ReadOnlyCollection<object>(key);
        }

        /// <summary>
        /// Gets the table where the exception occurred.
        /// </summary>
        public string Table { get; }

        /// <summary>
        /// Gets the key that caused the exception.
        /// </summary>
        public ReadOnlyCollection<object> Key { get; }
    }
}
