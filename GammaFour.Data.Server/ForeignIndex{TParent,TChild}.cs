// <copyright file="ForeignIndex{TParent,TChild}.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary>
    /// A foreign index.
    /// </summary>
    /// <typeparam name="TParent">The parent type.</typeparam>
    /// <typeparam name="TChild">The child type.</typeparam>
    public class ForeignIndex<TParent, TChild> : ForeignIndex
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

        /// <summary>
        /// Initializes a new instance of the <see cref="ForeignIndex{TParent, TChild}"/> class.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="parentIndex">The parent index.</param>
        public ForeignIndex(string name, IUniqueIndex parentIndex)
            : base(name, parentIndex)
        {
        }

        /// <inheritdoc/>
        public override bool Filter(IRow row)
        {
            // This will typically be a test for null.
            return this.filterFunction(row as TChild);
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
            // Extract the key from the row.
            return this.keyFunction(row as TChild);
        }

        /// <summary>
        /// Gets the parent recordd of the given child.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public new TParent GetParent(IRow child)
        {
            // Find the parent record.
            return base.GetParent(child) as TParent;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filter">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public ForeignIndex<TParent, TChild> HasFilter(Expression<Func<TChild, bool>> filter)
        {
            this.filterFunction = filter.Compile();
            return this;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="key">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public ForeignIndex<TParent, TChild> HasIndex(Expression<Func<TChild, object>> key)
        {
            this.keyFunction = key.Compile();
            return this;
        }
    }
}