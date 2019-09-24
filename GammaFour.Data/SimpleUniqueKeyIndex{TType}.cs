// <copyright file="SimpleUniqueKeyIndex{TType}.cs" company="Gamma Four, Inc.">
//    Copyright © 2018 - Gamma Four, Inc.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    /// <summary>
    /// A unique index without the transaction logic.
    /// </summary>
    /// <typeparam name="TType">The value.</typeparam>
    public class SimpleUniqueKeyIndex<TType>
        where TType : IVersionable<TType>
    {
        /// <summary>
        /// The dictionary mapping the keys to the values.
        /// </summary>
        private Dictionary<object, TType> dictionary;

        /// <summary>
        /// Used to filter items that appear in the index.
        /// </summary>
        private Func<TType, bool> filterFunction = t => true;

        /// <summary>
        /// Used to get the primary key from the record.
        /// </summary>
        private Func<TType, object> keyFunction;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleUniqueKeyIndex{TType}"/> class.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        public SimpleUniqueKeyIndex(string name)
        {
            // Initialize the object.
            this.Name = name;
            this.dictionary = new Dictionary<object, TType>();
        }

        /// <summary>
        /// Gets or sets the handler for when the index is changed.
        /// </summary>
        public EventHandler<RecordChangeEventArgs<object>> IndexChangedHandler { get; set; }

        /// <summary>
        /// Gets a lock used to synchronize multithreaded access.
        /// </summary>
        public AsyncReaderWriterLock Lock { get; } = new AsyncReaderWriterLock();

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets a value that indicates if the index contains the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>True if the index contains the given key, false otherwise.</returns>
        public bool ContainsKey(object key)
        {
            // Determine if the index holds the given key.
            return this.dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Finds the value indexed by the given key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        public TType Find(object key)
        {
            // Return the value from the dictionary, or null if it doesn't exist.
            TType value;
            this.dictionary.TryGetValue(key, out value);
            return value;
        }

        /// <summary>
        /// Gets the key of the given record.
        /// </summary>
        /// <param name="value">The record.</param>
        /// <returns>The key values.</returns>
        public object GetKey(TType value)
        {
            return this.keyFunction(value);
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="key">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public SimpleUniqueKeyIndex<TType> HasIndex(Expression<Func<TType, object>> key)
        {
            this.keyFunction = key.Compile();
            return this;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filter">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public SimpleUniqueKeyIndex<TType> HasFilter(Expression<Func<TType, bool>> filter)
        {
            this.filterFunction = filter.Compile();
            return this;
        }

        /// <summary>
        /// Adds a key to the index.
        /// </summary>
        /// <param name="value">The referenced record.</param>
        public void Add(TType value)
        {
            // Extract the key from the value and add it to the dictionary making sure we can undo the action.
            if (this.filterFunction(value))
            {
                object key = this.keyFunction(value);
                this.dictionary.Add(key, value);
                this.OnIndexChanging(DataAction.Add, null, key);
            }
        }

        /// <summary>
        /// Removes a key from the index.
        /// </summary>
        /// <param name="value">The record to be removed.</param>
        public void Remove(TType value)
        {
            // Make sure the key was properly removed before we push an undo operation on the stack.  Removing an item that isn't part of the index
            // is not considered an exception.
            object key = this.keyFunction(value);
            if (this.dictionary.Remove(key))
            {
                this.OnIndexChanging(DataAction.Delete, key, null);
            }
        }

        /// <summary>
        /// Updates the key of a record in the index.
        /// </summary>
        /// <param name="value">The record that has changed.</param>
        public void Update(TType value)
        {
            // There's nothing to update if the key hasn't changed.
            TType oldValue = value.GetVersion(RecordVersion.Previous);
            object oldKey = this.keyFunction(oldValue);
            object newKey = this.keyFunction(value);

            // Nothing to do if the keys are the same.
            if (oldKey != null && oldKey.Equals(newKey))
            {
                return;
            }

            // Remove the old key.
            if (this.filterFunction(oldValue))
            {
                this.dictionary.Remove(oldKey);
            }

            // Extract the new key from the value and add it to the dictionary.
            if (this.filterFunction(value))
            {
                this.dictionary.Add(newKey, value);
            }

            // Notify when the index has changed.
            this.OnIndexChanging(DataAction.Update, oldKey, newKey);
        }

        /// <summary>
        /// Handles the changing of the index.
        /// </summary>
        /// <param name="dataAction">The action performed (Add, Update, Delete).</param>
        /// <param name="oldKey">The old key.</param>
        /// <param name="newKey">The new key.</param>
        private void OnIndexChanging(DataAction dataAction, object oldKey, object newKey)
        {
            this.IndexChangedHandler?.Invoke(this, new RecordChangeEventArgs<object>() { Current = newKey, DataAction = dataAction, Previous = oldKey });
        }
    }
}