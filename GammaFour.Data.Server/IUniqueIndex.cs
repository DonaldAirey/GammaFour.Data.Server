// <copyright file="IUniqueIndex.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Transactions;

    /// <summary>
    /// An interface for a unique index.
    /// </summary>
    public interface IUniqueIndex : IEnlistmentNotification, ILockable
    {
        /// <summary>
        /// Gets or sets the handler for when the index is changed.
        /// </summary>
        EventHandler<RecordChangeEventArgs<IRow>> IndexChangedHandler { get; set; }

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets or sets the table to which this index belongs.
        /// </summary>
        ITable Table { get; set; }

        /// <summary>
        /// Adds a key to the index.
        /// </summary>
        /// <param name="row">The referenced record.</param>
        void Add(IRow row);

        /// <summary>
        /// Gets an indication whether the index contains the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True if the index contains the given key, false otherwise.</returns>
        bool ContainsKey(object key);

        /// <summary>
        /// Determines if a given row belongs in the index.
        /// </summary>
        /// <param name="row">the row to be evaluated.</param>
        /// <returns>true if the row belongs in the index, false if not.</returns>
        bool Filter(IRow row);

        /// <summary>
        /// Finds the row indexed by the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        IRow Find(object key);

        /// <summary>
        /// Gets the key of the given record.
        /// </summary>
        /// <param name="row">The record.</param>
        /// <returns>The key rows.</returns>
        object GetKey(IRow row);

        /// <summary>
        /// Removes a key from the index.
        /// </summary>
        /// <param name="row">The record to be removed.</param>
        void Remove(IRow row);

        /// <summary>
        /// Updates the key of a record in the index.
        /// </summary>
        /// <param name="row">The record that has changed.</param>
        void Update(IRow row);
    }
}