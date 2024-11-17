// <copyright file="UniqueIndex{T}.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;

    /// <summary>
    /// A unique index.
    /// </summary>
    /// <typeparam name="T">The type of IRow managed by the index.</typeparam>
    /// <param name="name">The name of the index.</param>
    public class UniqueIndex<T>(string name)
        : UniqueIndex(name)
        where T : class, IRow
    {
        /// <summary>
        /// Gets or sets a function used to filter items that should not appear in the index.
        /// </summary>
        private Func<T, bool> filterFunction = t => true;

        /// <summary>
        /// Gets or sets the function used to get the primary key from the record.
        /// </summary>
        private Func<T, object> keyFunction = t => throw new NotImplementedException();

        /// <inheritdoc/>
        public override bool Filter(IRow row)
        {
            // Validate the arguments.
            var typedRow = row as T;
            ArgumentNullException.ThrowIfNull(typedRow);

            // This will typically be a test for null.
            return this.filterFunction(typedRow);
        }

        /// <summary>
        /// Finds the row indexed by the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        public new T Find(object key)
        {
            // Validate the arguments.
            var typedKey = base.Find(key) as T;
            ArgumentNullException.ThrowIfNull(typedKey);

            // Return the row from the dictionary, or null if it doesn't exist.
            return typedKey;
        }

        /// <inheritdoc/>
        public override object GetKey(IRow row)
        {
            // Validate the arguments.
            var typedRow = row as T;
            ArgumentNullException.ThrowIfNull(typedRow);

            // Return the key function for the given row.
            return this.keyFunction(typedRow);
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filterFunction">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this IRow for Fluent construction.</returns>
        public UniqueIndex<T> HasFilter(Func<T, bool> filterFunction)
        {
            this.filterFunction = filterFunction;
            return this;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="keyFunction">Used to extract the key from the record.</param>
        /// <returns>A reference to this IRow for Fluent construction.</returns>
        public UniqueIndex<T> HasIndex(Func<T, object> keyFunction)
        {
            this.keyFunction = keyFunction;
            return this;
        }
    }
}