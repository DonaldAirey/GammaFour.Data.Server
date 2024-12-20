﻿// <copyright file="ForeignIndex{TParent,TChild}.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A foreign index.
    /// </summary>
    /// <typeparam name="TParent">The parent type.</typeparam>
    /// <typeparam name="TChild">The child type.</typeparam>
    /// <param name="name">The name of the index.</param>
    /// <param name="parentIndex">The parent index.</param>
    public class ForeignIndex<TParent, TChild>(string name, IUniqueIndex parentIndex)
        : ForeignIndex(name, parentIndex)
        where TParent : class
        where TChild : class
    {
        /// <summary>
        /// Gets or sets a function to filter items that appear in the index.
        /// </summary>
        private Func<TChild, bool> filterFunction = t => true;

        /// <summary>
        /// Gets or sets a function used to get the key from the child record.
        /// </summary>
        private Func<TChild, object> keyFunction = t => throw new NotImplementedException();

        /// <inheritdoc/>
        public override bool Filter(IRow row)
        {
            // Validate the arguments.
            ArgumentNullException.ThrowIfNull(row);
            var child = row as TChild;
            ArgumentNullException.ThrowIfNull(child);

            // This will typically be a test for null.
            return this.filterFunction(child);
        }

        /// <summary>
        /// Finds the value indexed by the given key.
        /// </summary>
        /// <param name="parent">The parent record.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        public new IEnumerable<TChild> GetChildren(IRow parent)
        {
            // Return the list of children for the given parent record, or an empty list if there are no children.
            return base.GetChildren(parent).Cast<TChild>();
        }

        /// <inheritdoc/>
        public override object GetKey(IRow row)
        {
            // Validate the arguments.
            ArgumentNullException.ThrowIfNull(row);
            var child = row as TChild;
            ArgumentNullException.ThrowIfNull(child);

            // Extract the key from the row.
            return this.keyFunction(child);
        }

        /// <summary>
        /// Gets the parent recordd of the given child.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public new TParent? GetParent(IRow child)
        {
            // Validate the arguments.
            ArgumentNullException.ThrowIfNull(child);

            // Return the parent.
            return base.GetParent(child) as TParent;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filterFunction">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public ForeignIndex<TParent, TChild> HasFilter(Func<TChild, bool> filterFunction)
        {
            this.filterFunction = filterFunction;
            return this;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="keyFunction">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public ForeignIndex<TParent, TChild> HasIndex(Func<TChild, object> keyFunction)
        {
            this.keyFunction = keyFunction;
            return this;
        }
    }
}