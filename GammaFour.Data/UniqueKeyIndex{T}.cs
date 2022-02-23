// <copyright file="UniqueKeyIndex{T}.cs" company="Donald Roy Airey">
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
    /// A unique index.
    /// </summary>
    /// <typeparam name="T">The value.</typeparam>
    public class UniqueKeyIndex<T> : IEnlistmentNotification, ILockable
        where T : IVersionable<T>
    {
        /// <summary>
        /// Gets a lock used to synchronize multithreaded access.
        /// </summary>
        private readonly AsyncReaderWriterLock asyncReaderWriterLock = new ();

        /// <summary>
        /// The dictionary mapping the keys to the values.
        /// </summary>
        private readonly Dictionary<object, T> dictionary = new ();

        /// <summary>
        /// The actions for undoing a transaction.
        /// </summary>
        private readonly Stack<Action> undoStack = new ();

        /// <summary>
        /// Used to filter items that appear in the index.
        /// </summary>
        private Func<T, bool> filterFunction = t => true;

        /// <summary>
        /// Used to get the primary key from the record.
        /// </summary>
        private Func<T, object> keyFunction = t => throw new NotImplementedException();

        /// <summary>
        /// Initializes a new instance of the <see cref="UniqueKeyIndex{TType}"/> class.
        /// </summary>
        /// <param name="name">The name of the index.</param>
        public UniqueKeyIndex(string name)
        {
            // Initialize the object.
            this.Name = name;
        }

        /// <summary>
        /// Gets or sets the handler for when the index is changed.
        /// </summary>
        public EventHandler<RecordChangeEventArgs<object>>? IndexChangedHandler { get; set; }

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Adds a key to the index.
        /// </summary>
        /// <param name="value">The referenced record.</param>
        public void Add(T value)
        {
            // For those values that qualify as keys, extract the key from the record and add it to the dictionary while making sure we can undo the
            // action.
            if (this.filterFunction(value))
            {
                object key = this.keyFunction(value);
                this.dictionary.Add(key, value);
                this.undoStack.Push(() => this.dictionary.Remove(key));

                // This is used to notify a foreign key that this value has changed.  The parent will have the opportunity to reject the change if it
                // violates referential integrity.
                this.OnIndexChanging(DataAction.Add, key, null);
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
        public T? Find(object key)
        {
            // Return the value from the dictionary, or null if it doesn't exist.
            return this.dictionary.TryGetValue(key, out T? value) ? value : default;
        }

        /// <summary>
        /// Gets the key of the given record.
        /// </summary>
        /// <param name="value">The record.</param>
        /// <returns>The key values.</returns>
        public object GetKey(T value)
        {
            return this.keyFunction(value);
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="filter">Used to filter items that appear in the index.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public UniqueKeyIndex<T> HasFilter(Expression<Func<T, bool>> filter)
        {
            this.filterFunction = filter.Compile();
            return this;
        }

        /// <summary>
        /// Specifies the key for organizing the collection.
        /// </summary>
        /// <param name="key">Used to extract the key from the record.</param>
        /// <returns>A reference to this object for Fluent construction.</returns>
        public UniqueKeyIndex<T> HasIndex(Expression<Func<T, object>> key)
        {
            this.keyFunction = key.Compile();
            return this;
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
            // the second phase.
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
            // Releases the lock.
            this.asyncReaderWriterLock.Release();
        }

        /// <summary>
        /// Removes a key from the index.
        /// </summary>
        /// <param name="value">The record to be removed.</param>
        public void Remove(T value)
        {
            // Make sure the key was properly removed before we push an undo operation on the stack.  Removing an item that isn't part of the index
            // is not considered an exception.
            if (this.filterFunction(value))
            {
                object key = this.keyFunction(value);
                if (this.dictionary.Remove(key))
                {
                    this.undoStack.Push(() => this.dictionary.Add(key, value));
                    this.OnIndexChanging(DataAction.Delete, null, key);
                }
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
        /// Updates the key of a record in the index.
        /// </summary>
        /// <param name="value">The record that has changed.</param>
        public void Update(T value)
        {
            T previousValue = value.GetVersion(RecordVersion.Previous);
            object previousKey = this.keyFunction(previousValue);
            object currentKey = this.keyFunction(value);

            // Update should only perform work when the values are different.
            if (!object.Equals(previousKey, currentKey))
            {
                // Make sure the key was properly removed before we push an undo operation on the stack.  Removing an item that isn't part of the
                // index is not considered an exception.
                if (this.filterFunction(previousValue))
                {
                    if (this.dictionary.Remove(previousKey))
                    {
                        this.undoStack.Push(() => this.dictionary.Add(previousKey, value));
                    }
                }

                // Extract the new key from the value and add it to the dictionary making sure we can undo the action.
                if (this.filterFunction(value))
                {
                    this.dictionary.Add(currentKey, value);
                    this.undoStack.Push(() => this.dictionary.Remove(currentKey));
                }

                // Notify when the index has changed.
                this.OnIndexChanging(DataAction.Update, previousKey, currentKey);
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
        /// Handles the changing of the index.
        /// </summary>
        /// <param name="dataAction">The action performed (Add, Update, Delete).</param>
        /// <param name="previous">The previous value of the key.</param>
        /// <param name="current">The current value of the key.</param>
        private void OnIndexChanging(DataAction dataAction, object? previous, object? current)
        {
            this.IndexChangedHandler?.Invoke(this, new RecordChangeEventArgs<object>(dataAction, previous, current));
        }
    }
}