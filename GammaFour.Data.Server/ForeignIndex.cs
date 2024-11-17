// <copyright file="ForeignIndex.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using DotNext.Threading;

    /// <summary>
    /// A foreign index.
    /// </summary>
    public class ForeignIndex : IForeignIndex
    {
        /// <summary>
        /// Gets a lock used to synchronize multithreaded access.
        /// </summary>
        private readonly AsyncReaderWriterLock asyncReaderWriterLock = new AsyncReaderWriterLock();

        /// <summary>
        /// The dictionary containing the index.
        /// </summary>
        private readonly Dictionary<object, HashSet<IRow>> dictionary = new Dictionary<object, HashSet<IRow>>();

        /// <summary>
        /// The actions for undoing a transaction.
        /// </summary>
        private readonly Stack<Action> undoStack = new Stack<Action>();

        /// <summary>
        /// Gets or sets a function to filter items that appear in the index.
        /// </summary>
        private Func<IRow, bool> filterFunction = t => true;

        /// <summary>
        /// Gets or sets a function used to get the key from the child record.
        /// </summary>
        private Func<IRow, object> keyFunction = t => throw new NotImplementedException();

        /// <summary>
        /// Initializes a new instance of the <see cref="ForeignIndex"/> class.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        /// <param name="parentIndex">The parent index.</param>
        public ForeignIndex(string name, IUniqueIndex parentIndex)
        {
            // Initialize the object.
            this.Name = name;
            this.UniqueIndex = parentIndex;

            // This instructs the parent key to inform this object about any changes.
            this.UniqueIndex.IndexChangedHandler += this.HandleUniqueIndexChange;
        }

        /// <inheritdoc/>
        public bool IsReadLockHeld => this.asyncReaderWriterLock.IsReadLockHeld;

        /// <inheritdoc/>
        public bool IsWriteLockHeld => this.asyncReaderWriterLock.IsWriteLockHeld;

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public IUniqueIndex UniqueIndex { get; }

        /// <summary>
        /// Adds a key to the index.
        /// </summary>
        /// <param name="row">The referenced record.</param>
        public void Add(IRow row)
        {
            // For those values that qualify as keys, extract the key from the record and add it to the dictionary making sure we can undo the action.
            if (this.Filter(row))
            {
                // Don't attempt to add a record with a null key.
                object key = this.GetKey(row);

                // Make sure the new key exist in the parent.
                if (!this.UniqueIndex.ContainsKey(key))
                {
                    throw new KeyNotFoundException($"{this.Name}: {key}");
                }

                // Find or create a bucket of child records for the new key.
                if (!this.dictionary.TryGetValue(key, out HashSet<IRow>? hashSet))
                {
                    hashSet = new HashSet<IRow>();
                    this.dictionary.Add(key, hashSet);
                }

                // Add the key to the index and make sure it's unique.
                if (!hashSet.Add(row))
                {
                    throw new DuplicateKeyException($"{this.Name}: {key}");
                }

                // This allows us to back out of the operation.
                this.undoStack.Push(() =>
                {
                    // Undoing this operation involves removing the new child from the index to the parent table.  If this is the last item child
                    // referencing the parent table, then remove the hashset.
                    HashSet<IRow> undoHashSet = this.dictionary[key];
                    undoHashSet.Remove(row);
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

        /// <inheritdoc/>
        public virtual bool Filter(IRow row)
        {
            // This will typically be a test for null.
            return this.filterFunction(row);
        }

        /// <summary>
        /// Finds the row indexed by the given key.
        /// </summary>
        /// <param name="parent">The parent record.</param>
        /// <returns>The record indexed by the given key, or null if it doesn't exist.</returns>
        public IEnumerable<IRow> GetChildren(IRow parent)
        {
            // Return the list of children for the given parent record, or an empty list if there are no children.
            var key = this.UniqueIndex.GetKey(parent);
            if (key != null)
            {
                if (this.dictionary.TryGetValue(key, out HashSet<IRow>? rows))
                {
                    return rows;
                }
            }

            return Enumerable.Empty<IRow>();
        }

        /// <inheritdoc/>
        public virtual object GetKey(IRow row)
        {
            return this.keyFunction(row);
        }

        /// <summary>
        /// Gets the parent record of the given child.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public IRow? GetParent(IRow child)
        {
            // Find the parent record.
            return this.Filter(child) ? this.UniqueIndex.Find(this.GetKey(child)) : null;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filterFunction">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public IForeignIndex HasFilter(Func<IRow, bool> filterFunction)
        {
            this.filterFunction = filterFunction;
            return this;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="keyFunction">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public IForeignIndex HasIndex(Func<IRow, object> keyFunction)
        {
            this.keyFunction = keyFunction;
            return this;
        }

        /// <summary>
        /// Gets an indication of whether the child record has a parent.
        /// </summary>
        /// <param name="child">The child record.</param>
        /// <returns>The parent record of the given child.</returns>
        public bool HasParent(IRow child)
        {
            // Return the parent record.
            return !this.Filter(child) || this.UniqueIndex.Find(this.GetKey(child)) != null;
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
        /// <param name="row">The the row.</param>
        public void Remove(IRow row)
        {
            // Only attempt to remove a child if the child has a valid key referencing the parent.
            if (this.Filter(row))
            {
                // Get the current property from the child that references a unique key on the parent.
                object key = this.GetKey(row);

                // Find the set of child records belonging to the given parent that has the key extracted from the child.
                if (this.dictionary.TryGetValue(key, out HashSet<IRow>? hashSet))
                {
                    // Remove the existing child record from the hash and remove the hash if it's empty.
                    hashSet.Remove(row);
                    if (hashSet.Count == 0)
                    {
                        this.dictionary.Remove(key);
                    }

                    // This allows us to back out of the operation.
                    this.undoStack.Push(() =>
                    {
                        // Make sure there's a bucket for the restored record.
                        if (!this.dictionary.TryGetValue(key, out HashSet<IRow>? undoHashSet))
                        {
                            undoHashSet = new HashSet<IRow>();
                            this.dictionary.Add(key, undoHashSet);
                        }

                        // This will place the restored record back in the hashtable.
                        undoHashSet.Add(row);
                    });
                }
            }
        }

        /// <summary>
        /// Changes a key row.
        /// </summary>
        /// <param name="row">The new record.</param>
        public void Update(IRow row)
        {
            // If the key to this index hasn't changed from the previous row, then there's nothing to do here.
            IRow previousRow = row.GetVersion(RecordVersion.Previous);
            object previousKey = this.GetKey(previousRow);

            // If the key to this index hasn't changed from the previous row, then there's nothing to do here.
            object currentKey = this.GetKey(row);
            if (!object.Equals(currentKey, previousKey))
            {
                // Only remove the previous record from the index if it has a valid key referencing the parent table.
                if (this.Filter(previousRow))
                {
                    // Make sure the previous exist in the index before removing the child.
                    if (!this.dictionary.TryGetValue(previousKey, out var hashSet))
                    {
                        throw new KeyNotFoundException($"{this.Name}: {previousKey}");
                    }

                    // Remove the existing child record from the hash and remove the hash from the dictionary if it's empty.
                    hashSet.Remove(row);
                    if (hashSet.Count == 0)
                    {
                        this.dictionary.Remove(previousKey);
                    }

                    // This allows us to back out of the operation.
                    this.undoStack.Push(() =>
                    {
                        // Make sure there's a bucket for the restored record.
                        if (!this.dictionary.TryGetValue(previousKey, out HashSet<IRow>? undoHashSet))
                        {
                            undoHashSet = new HashSet<IRow>();
                            this.dictionary.Add(previousKey, undoHashSet);
                        }

                        // This will place the restored record back in the hashtable.
                        undoHashSet.Add(row);
                    });
                }

                // Only add the current record to the index if it has a valid key referencing the parent table.
                if (this.Filter(row))
                {
                    // Don't attempt to add a record with a null key.
                    object newKey = this.GetKey(row);

                    // Make sure the new key exist in the parent.
                    if (!this.UniqueIndex.ContainsKey(newKey))
                    {
                        throw new KeyNotFoundException($"{this.Name}: {newKey}");
                    }

                    // Find or create a bucket of child records for the new newKey.
                    if (!this.dictionary.TryGetValue(newKey, out var hashSet))
                    {
                        hashSet = new HashSet<IRow>();
                        this.dictionary.Add(newKey, hashSet);
                    }

                    // Add the newKey to the index and make sure it's unique.
                    if (!hashSet.Add(row))
                    {
                        throw new DuplicateKeyException($"{this.Name}: {newKey}");
                    }

                    // This allows us to back out of the operation.
                    this.undoStack.Push(() =>
                    {
                        // Remove the new child from the index.
                        HashSet<IRow> undoHashSet = this.dictionary[newKey];
                        undoHashSet.Remove(row);
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
        private void HandleUniqueIndexChange(object? sender, RecordChangeEventArgs<IRow> recordChangeEventArgs)
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