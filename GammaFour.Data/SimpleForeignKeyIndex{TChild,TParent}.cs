// <copyright file="SimpleForeignKeyIndex{TChild,TParent}.cs" company="Donald Roy Airey">
//    Copyright © 2020 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Transactions;
    using Microsoft.VisualStudio.Threading;

    /// <summary>
    /// An foreign key index without the transaction logic.
    /// </summary>
    /// <typeparam name="TChild">The child value.</typeparam>
    /// <typeparam name="TParent">The parent value.</typeparam>
    public class SimpleForeignKeyIndex<TChild, TParent>
        where TParent : IVersionable<TParent>
        where TChild : IVersionable<TChild>
    {
        /// <summary>
        /// The dictionary containing the index.
        /// </summary>
        private Dictionary<object, HashSet<TChild>> dictionary;

        /// <summary>
        /// Used to get the primary key from the record.
        /// </summary>
        private Func<TChild, object> keyFunction;

        /// <summary>
        /// The parent index.
        /// </summary>
        private SimpleUniqueKeyIndex<TParent> parentIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleForeignKeyIndex{TChild, TParent}"/> class.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="parentIndex">The parent index.</param>
        public SimpleForeignKeyIndex(string name, SimpleUniqueKeyIndex<TParent> parentIndex)
        {
            // Initialize the object.
            this.Name = name;
            this.parentIndex = parentIndex;
            this.dictionary = new Dictionary<object, HashSet<TChild>>();

            // Validate the argument.
            if (parentIndex == null)
            {
                throw new ArgumentNullException(nameof(parentIndex));
            }

            // This instructs the parent key to inform this object about any changes.
            this.parentIndex.IndexChangedHandler += this.HandleUniqueIndexChange;
        }

        /// <summary>
        /// Gets a lock used to synchronize multithreaded access.
        /// </summary>
        public AsyncReaderWriterLock Lock { get; } = new AsyncReaderWriterLock();

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Adds a key to the index.
        /// </summary>
        /// <param name="value">The referenced record.</param>
        public void Add(TChild value)
        {
            // Don't attempt to add a record with a null key.
            object key = this.keyFunction(value);
            if (key != null)
            {
                // Make sure the new key exist in the parent.
                if (!this.parentIndex.ContainsKey(key))
                {
                    throw new KeyNotFoundException($"{this.Name}: {key}");
                }

                // Find or create a bucket of child records for the new key.
                HashSet<TChild> hashSet;
                if (!this.dictionary.TryGetValue(key, out hashSet))
                {
                    hashSet = new HashSet<TChild>();
                    this.dictionary.Add(key, hashSet);
                }

                // Add the key to the index and make sure it's unique.
                if (!hashSet.Add(value))
                {
                    throw new DuplicateKeyException($"{this.Name}: {key}");
                }
            }
        }

        /// <summary>
        /// Finds the value indexed by the given key.
        /// </summary>
        /// <param name="parent">The parent record.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        public IEnumerable<TChild> GetChildren(TParent parent)
        {
            // If there is no entry in the dictionary, return an empty list.
            HashSet<TChild> value;
            object key = this.parentIndex.GetKey(parent);
            if (!this.dictionary.TryGetValue(key, out value))
            {
                return new List<TChild>();
            }

            // Return the set of children.
            return value;
        }

        /// <summary>
        /// Gets the parent recordd of the given child.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public TParent GetParent(TChild child)
        {
            // Return the parent record.
            object key = this.keyFunction(child);
            return key == null ? default(TParent) : this.parentIndex.Find(key);
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="key">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public SimpleForeignKeyIndex<TChild, TParent> HasIndex(Expression<Func<TChild, object>> key)
        {
            // Validate the argument.
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.keyFunction = key.Compile();
            return this;
        }

        /// <summary>
        /// Gets the parent recordd of the given child.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public bool HasParent(TChild child)
        {
            // Return the parent record.
            object key = this.keyFunction(child);
            return key == null ? true : this.parentIndex.Find(key) == null ? false : true;
        }

        /// <summary>
        /// Removes a key from the index.
        /// </summary>
        /// <param name="value">The the value.</param>
        public void Remove(TChild value)
        {
            // Don't attempt to remove a record with a null key.
            object key = this.keyFunction(value);
            if (key != null)
            {
                // Make sure the parent key exist in the index before removing the child.  If the parent doesn't exist, then there's not much we can
                // do.
                HashSet<TChild> hashSet;
                if (!this.dictionary.TryGetValue(key, out hashSet))
                {
                    return;
                }

                // Remove the existing child record from the hash and remove the hash if it's empty.
                hashSet.Remove(value);
                if (hashSet.Count == 0)
                {
                    this.dictionary.Remove(key);
                }
            }
        }

        /// <summary>
        /// Changes a key value.
        /// </summary>
        /// <param name="value">The new record.</param>
        public void Update(TChild value)
        {
            // If the key to this index hasn't changed from the previous value, then there's nothing to do here.
            object oldKey = this.keyFunction(value.GetVersion(RecordVersion.Previous));
            object key = this.keyFunction(value);
            if (oldKey != null && oldKey.Equals(key))
            {
                return;
            }

            // Don't attempt to remove a record with a null key because it was never put here in the first place.
            if (oldKey != null)
            {
                // Make sure the parent oldKey exist in the index before removing the child.
                HashSet<TChild> hashSet;
                if (!this.dictionary.TryGetValue(oldKey, out hashSet))
                {
                    throw new KeyNotFoundException($"{this.Name}: {oldKey}");
                }

                // Remove the existing child record from the hash and remove the hash if it's empty.
                hashSet.Remove(value);
                if (hashSet.Count == 0)
                {
                    this.dictionary.Remove(oldKey);
                }
            }

            // Don't attempt to add a record with a null key.
            object newKey = this.keyFunction(value);
            if (newKey != null)
            {
                // Make sure the new key exist in the parent.
                if (!this.parentIndex.ContainsKey(newKey))
                {
                    throw new KeyNotFoundException($"{this.Name}: {newKey}");
                }

                // Find or create a bucket of child records for the new newKey.
                HashSet<TChild> hashSet;
                if (!this.dictionary.TryGetValue(newKey, out hashSet))
                {
                    hashSet = new HashSet<TChild>();
                    this.dictionary.Add(newKey, hashSet);
                }

                // Add the newKey to the index and make sure it's unique.
                if (!hashSet.Add(value))
                {
                    throw new DuplicateKeyException($"{this.Name}: {newKey}");
                }
            }
        }

        /// <summary>
        /// Handles a change in the parent unique index.
        /// </summary>
        /// <param name="sender">The originator of the event.</param>
        /// <param name="recordChangeEventArgs">The event arguments.</param>
        private void HandleUniqueIndexChange(object sender, RecordChangeEventArgs<object> recordChangeEventArgs)
        {
            if ((recordChangeEventArgs.DataAction == DataAction.Delete || recordChangeEventArgs.DataAction == DataAction.Update)
                && this.dictionary.ContainsKey(recordChangeEventArgs.Previous))
            {
                throw new ConstraintException($"Attempt to delete {recordChangeEventArgs.Previous}.", this.Name);
            }
        }
    }
}