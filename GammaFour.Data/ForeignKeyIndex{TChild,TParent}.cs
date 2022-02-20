// <copyright file="ForeignKeyIndex{TChild,TParent}.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using DotNext.Threading;

    /// <summary>
    /// An index.
    /// </summary>
    /// <typeparam name="TChild">The child value.</typeparam>
    /// <typeparam name="TParent">The parent value.</typeparam>
    public class ForeignKeyIndex<TChild, TParent> : IEnlistmentNotification, ILockable
        where TParent : IVersionable<TParent>
        where TChild : IVersionable<TChild>
    {
        /// <summary>
        /// Gets a lock used to synchronize multithreaded access.
        /// </summary>
        private readonly AsyncReaderWriterLock asyncReaderWriterLock = new ();

        /// <summary>
        /// The dictionary containing the index.
        /// </summary>
        private readonly Dictionary<object, HashSet<TChild>> dictionary = new ();

        /// <summary>
        /// The unique parent index.
        /// </summary>
        private readonly UniqueKeyIndex<TParent> parentIndex;

        /// <summary>
        /// The actions for undoing a transaction.
        /// </summary>
        private readonly Stack<Action> undoStack = new ();

        /// <summary>
        /// Used to filter items that appear in the index.
        /// </summary>
        private Func<TChild, bool> filterFunction = t => true;

        /// <summary>
        /// Used to get the key from the child record.
        /// </summary>
        private Func<TChild, object> keyFunction = t => throw new NotImplementedException();

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

            // This instructs the parent key to inform this object about any changes.
            this.parentIndex.IndexChangedHandler += this.HandleUniqueIndexChange;
        }

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
            // For those values that qualify as keys, extract the key from the record and add it to the dictionary making sure we can undo the action.
            if (this.filterFunction(value))
            {
                // Don't attempt to add a record with a null key.
                object key = this.keyFunction(value);

                // Make sure the new key exist in the parent.
                if (!this.parentIndex.ContainsKey(key))
                {
                    throw new KeyNotFoundException($"{this.Name}: {key}");
                }

                // Find or create a bucket of child records for the new key.
                if (!this.dictionary.TryGetValue(key, out HashSet<TChild>? hashSet))
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
                    // Undoing this operation involves removing the new child from the index to the parent table.  If this is the last item child
                    // referencing the parent table, then remove the hashset.
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
            // We don't need this after committing the transaction.
            this.undoStack.Clear();

            // The transaction is complete for this index.
            enlistment.Done();
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filter">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public ForeignKeyIndex<TChild, TParent> HasFilter(Expression<Func<TChild, bool>> filter)
        {
            this.filterFunction = filter.Compile();
            return this;
        }

        /// <summary>
        /// Finds the value indexed by the given key.
        /// </summary>
        /// <param name="parent">The parent record.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        public IEnumerable<TChild> GetChildren(TParent parent)
        {
            // Return the list of children for the given parent record, or an empty list if there are no children.
            return this.dictionary.TryGetValue(this.parentIndex.GetKey(parent), out HashSet<TChild>? value) ? value : Enumerable.Empty<TChild>();
        }

        /// <summary>
        /// Gets the parent recordd of the given child.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public TParent? GetParent(TChild child)
        {
            // Find the parent record.
            return this.filterFunction(child) ? this.parentIndex.Find(this.keyFunction(child)) : default;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="key">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public ForeignKeyIndex<TChild, TParent> HasIndex(Expression<Func<TChild, object>> key)
        {
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
            return !this.filterFunction(child) || this.parentIndex.Find(this.keyFunction(child)) != null;
        }

        /// <inheritdoc/>
        public void InDoubt(Enlistment enlistment)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public void Prepare(PreparingEnlistment preparingEnlistment)
        {
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
        public void Release()
        {
            // Releases the semaphore.
            this.asyncReaderWriterLock.Release();
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
            // Only attempt to remove a child if the child has a valid key referencing the parent.
            if (this.filterFunction(value))
            {
                // Get the current property from the child that references a unique key on the parent.
                object key = this.keyFunction(value);

                // Find the set of child records belonging to the given parent that has the key extracted from the child.
                if (this.dictionary.TryGetValue(key, out HashSet<TChild>? hashSet))
                {
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
                        if (!this.dictionary.TryGetValue(key, out HashSet<TChild>? undoHashSet))
                        {
                            undoHashSet = new HashSet<TChild>();
                            this.dictionary.Add(key, undoHashSet);
                        }

                        // This will place the restored record back in the hashtable.
                        undoHashSet.Add(value);
                    });
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
            TChild previousValue = value.GetVersion(RecordVersion.Previous);
            object previousKey = this.keyFunction(previousValue);
            object currentKey = this.keyFunction(value);
            if (!previousKey.Equals(currentKey))
            {
                // Only remove the previous record from the index if it has a valid key referencing the parent table.
                if (this.filterFunction(previousValue))
                {
                    // Make sure the previous exist in the index before removing the child.
                    if (!this.dictionary.TryGetValue(previousKey, out var hashSet))
                    {
                        throw new KeyNotFoundException($"{this.Name}: {previousKey}");
                    }

                    // Remove the existing child record from the hash and remove the hash from the dictionary if it's empty.
                    hashSet.Remove(value);
                    if (hashSet.Count == 0)
                    {
                        this.dictionary.Remove(previousKey);
                    }

                    // This allows us to back out of the operation.
                    this.undoStack.Push(() =>
                    {
                        // Make sure there's a bucket for the restored record.
                        if (!this.dictionary.TryGetValue(previousKey, out HashSet<TChild>? undoHashSet))
                        {
                            undoHashSet = new HashSet<TChild>();
                            this.dictionary.Add(previousKey, undoHashSet);
                        }

                        // This will place the restored record back in the hashtable.
                        undoHashSet.Add(value);
                    });
                }

                // Only add the current record to the index if it has a valid key referencing the parent table.
                if (this.filterFunction(value))
                {
                    // Don't attempt to add a record with a null key.
                    object newKey = this.keyFunction(value);

                    // Make sure the new key exist in the parent.
                    if (!this.parentIndex.ContainsKey(newKey))
                    {
                        throw new KeyNotFoundException($"{this.Name}: {newKey}");
                    }

                    // Find or create a bucket of child records for the new newKey.
                    if (!this.dictionary.TryGetValue(newKey, out var hashSet))
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
                        // Remove the new child from the index.
                        HashSet<TChild> undoHashSet = this.dictionary[newKey];
                        undoHashSet.Remove(value);
                        if (undoHashSet.Count == 0)
                        {
                            this.dictionary.Remove(newKey);
                        }
                    });
                }
            }
        }

        /// <inheritdoc/>
        public async Task WaitReaderAsync(CancellationToken cancellationToken)
        {
            // The semaphore is used to lock the object for the duration of a transaction, or until cancelled.
            await this.asyncReaderWriterLock.EnterReadLockAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task WaitWriterAsync(CancellationToken cancellationToken)
        {
            // The semaphore is used to lock the object for the duration of a transaction, or until cancelled.
            await this.asyncReaderWriterLock.EnterWriteLockAsync(cancellationToken);
        }

        /// <summary>
        /// Handles a change in the parent unique index.
        /// </summary>
        /// <param name="sender">The originator of the event.</param>
        /// <param name="recordChangeEventArgs">The event arguments.</param>
        private void HandleUniqueIndexChange(object? sender, RecordChangeEventArgs<object> recordChangeEventArgs)
        {
            // When deleting a parent record, or updating a parent record, enforce the constraint that the child records cannot be orphaned.
            if ((recordChangeEventArgs.DataAction == DataAction.Delete || recordChangeEventArgs.DataAction == DataAction.Update)
                && recordChangeEventArgs.Previous != null && this.dictionary.ContainsKey(recordChangeEventArgs.Previous))
            {
                throw new ConstraintException($"Attempt to delete {recordChangeEventArgs.Previous}.", this.Name);
            }
        }
    }
}