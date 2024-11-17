// <copyright file="IForeignIndex.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System.Collections.Generic;
    using System.Transactions;

    /// <summary>
    /// A foreign index.
    /// </summary>
    public interface IForeignIndex : ILockable, IEnlistmentNotification
    {
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the unique index of the parent.
        /// </summary>
        public IUniqueIndex UniqueIndex { get; }

        /// <summary>
        /// Adds a row to the index.
        /// </summary>
        /// <param name="row">The row.</param>
        void Add(IRow row);

        /// <summary>
        /// Determines if a given row belongs in the index.
        /// </summary>
        /// <param name="row">the row to be evaluated.</param>
        /// <returns>true if the row belongs in the index, false if not.</returns>
        bool Filter(IRow row);

        /// <summary>
        /// Gets the child records of the given parent row.
        /// </summary>
        /// <param name="parent">The parent row.</param>
        /// <returns>The rows that are related to the parent row.</returns>
        IEnumerable<IRow> GetChildren(IRow parent);

        /// <summary>
        /// Gets the key of the given record.
        /// </summary>
        /// <param name="row">The record.</param>
        /// <returns>The key rows.</returns>
        object GetKey(IRow row);

        /// <summary>
        /// Gets the parent row of the given child row.
        /// </summary>
        /// <param name="child">The child row.</param>
        /// <returns>The parent row of the given child.</returns>
        IRow? GetParent(IRow child);

        /// <summary>
        /// Gets an indication of whether the child row has a parent.
        /// </summary>
        /// <param name="child">The child row.</param>
        /// <returns>The parent row of the given child.</returns>
        bool HasParent(IRow child);

        /// <summary>
        /// Removes a key from the index.
        /// </summary>
        /// <param name="row">The the row.</param>
        void Remove(IRow row);

        /// <summary>
        /// Changes a key row.
        /// </summary>
        /// <param name="row">The new row.</param>
        void Update(IRow row);
    }
}