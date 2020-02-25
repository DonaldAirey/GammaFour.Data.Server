// <copyright file="ForeignKeyIndex{TChild,TParent}.cs" company="Donald Roy Airey">
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
    /// An index.
    /// </summary>
    /// <typeparam name="TChild">The child value.</typeparam>
    /// <typeparam name="TParent">The parent value.</typeparam>
    public class ForeignKeyIndex<TChild, TParent> : IEnlistmentNotification
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
        private UniqueKeyIndex<TParent> parentIndex;

        /// <summary>
        /// The actions for undoing a transaction.
        /// </summary>
        private Stack<Action> undoStack = new Stack<Action>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ForeignKeyIndex{TChild, TParent}"/> class.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="parentIndex">The parent index.</param>
        public ForeignKeyIndex(string name, UniqueKeyIndex<TParent> parentIndex)
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

                // This allows us to back out of the operation.
                this.undoStack.Push(() =>
                {
                    // Remove the new child from the index.  Note that we know it's there, so we don't need to 'try' and find it.
                    HashSet<TChild> undoHashSet = this.dictionary[key];
                    undoHashSet.Remove(value);
                    if (undoHashSet.Count == 0)
                    {
                        this.dictionary.Remove(key);
                    }
                });
            }
        }

        /// <inheritdoc/>
        public void Commit(Enlistment enlistment)
        {
            // Validate the argument.
            if (enlistment == null)
            {
                throw new ArgumentNullException(nameof(enlistment));
            }

            // We don't need this after committing the transaction.
            this.undoStack.Clear();

            // The transaction is complete as far as this index is concerned.
            enlistment.Done();
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
        public ForeignKeyIndex<TChild, TParent> HasIndex(Expression<Func<TChild, object>> key)
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
        /// Gets an indication of whether the child record has a parent.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public bool HasParent(TChild child)
        {
            // Return the parent record.
            object key = this.keyFunction(child);
            return key == null ? true : this.parentIndex.Find(key) == null ? false : true;
        }

        /// <inheritdoc/>
        public void InDoubt(Enlistment enlistment)
        {
            // Validate the argument.
            if (enlistment == null)
            {
                throw new ArgumentNullException(nameof(enlistment));
            }

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
            // Validate the argument.
            if (preparingEnlistment == null)
            {
                throw new ArgumentNullException(nameof(preparingEnlistment));
            }

            // If we haven't made any changes to the index, then just release any read locks.  Otherwise, signal that the transaction can proceed to
            // the second phase.  Note that we assume there aren't any write locks if there haven't been any changes.
            if (this.undoStack.Count == 0)
            {
                // We don't need to commit or roll back read-only actions.
                preparingEnlistment.Done();
            }
            else
            {
                // Ready to commit or rollback.
                preparingEnlistment.Prepared();
            }
        }

        /// <inheritdoc/>
        public void Rollback(Enlistment enlistment)
        {
            // Undo every action in the reverse order that it was enlisted.
            while (this.undoStack.Count != 0)
            {
                this.undoStack.Pop()();
            }
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

                // This allows us to back out of the operation.
                this.undoStack.Push(() =>
                {
                    // Make sure there's a bucket for the restored record.
                    HashSet<TChild> undoHashSet;
                    if (!this.dictionary.TryGetValue(key, out undoHashSet))
                    {
                        undoHashSet = new HashSet<TChild>();
                        this.dictionary.Add(key, undoHashSet);
                    }

                    // This will place the restored record back in the hashtable.
                    undoHashSet.Add(value);
                });
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

                // This allows us to back out of the operation.
                this.undoStack.Push(() =>
                {
                    // Make sure there's a bucket for the restored record.
                    HashSet<TChild> undoHashSet;
                    if (!this.dictionary.TryGetValue(oldKey, out undoHashSet))
                    {
                        undoHashSet = new HashSet<TChild>();
                        this.dictionary.Add(oldKey, undoHashSet);
                    }

                    // This will place the restored record back in the hashtable.
                    undoHashSet.Add(value);
                });
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

                // This allows us to back out of the operation.
                this.undoStack.Push(() =>
                {
                    // Remove the new child from the index.  Note that we know it's there, so we don't need to 'try' and find it.
                    HashSet<TChild> undoHashSet = this.dictionary[newKey];
                    undoHashSet.Remove(value);
                    if (undoHashSet.Count == 0)
                    {
                        this.dictionary.Remove(newKey);
                    }
                });
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