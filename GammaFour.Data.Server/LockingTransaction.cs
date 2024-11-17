// <copyright file="LockingTransaction.cs" company="Donald Roy Airey">
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

    /// <summary>
    /// An extension to the <see cref="TransactionScope"/> that also handles resource locking.
    /// </summary>
    public class LockingTransaction : IDisposable
    {
        /// <summary>
        /// Implicit cancellation token source for when non is supplied by the caller.
        /// </summary>
        private readonly CancellationTokenSource? cancellationTokenSource;

        /// <summary>
        /// The transaction.
        /// </summary>
        private readonly Transaction transaction;

        /// <summary>
        /// Provides a transactional code block.
        /// </summary>
        private readonly TransactionScope transactionScope;

        /// <summary>
        /// Collection of reader locks for the resources used by the transactional code block.
        /// </summary>
        private readonly HashSet<ILockable> readerLocks = new HashSet<ILockable>();

        /// <summary>
        /// Collection of writer locks for the resources used by the transactional code block.
        /// </summary>
        private readonly HashSet<ILockable> writerLocks = new HashSet<ILockable>();

        /// <summary>
        /// A cancellation token provided by the caller.
        /// </summary>
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="LockingTransaction"/> class.
        /// </summary>
        /// <param name="transactionTimeout">The TimeSpan after which the transaction scope times out and aborts the transaction.</param>
        public LockingTransaction(TimeSpan transactionTimeout = default)
        {
            // Create an implicit cancellation token based on a timeout.
            this.cancellationTokenSource = new CancellationTokenSource(transactionTimeout);
            this.cancellationToken = this.cancellationTokenSource.Token;

            // Initialize the object.
            this.transactionScope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionTimeout, TransactionScopeAsyncFlowOption.Enabled);

            // We must check for nulls to satisfy the compiler that Transaction.Current has a non-null value.
            if (Transaction.Current == default)
            {
                throw new Exception();
            }

            this.transaction = Transaction.Current;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LockingTransaction"/> class.
        /// </summary>
        /// <param name="cancellationToken">Used to cancel the entire transaction.</param>
        public LockingTransaction(CancellationToken cancellationToken)
        {
            // Initialize the object.
            this.cancellationToken = cancellationToken;

            // Initialize the object.
            this.transactionScope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled);

            // We must check for nulls to satisfy the compiler that Transaction.Current has a non-null value.
            if (Transaction.Current == default)
            {
                throw new Exception();
            }

            this.transaction = Transaction.Current;
        }

        /// <summary>
        /// Indicates that all operations within the scope are completed successfully.
        /// </summary>
        public void Complete()
        {
            // Commit all operations within the scope of this transaction.
            this.transactionScope.Complete();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Dispose of the managed resources and suppress finalization.
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously waits to read a protected resource.
        /// </summary>
        /// <param name="lockable">An object that can be locked for the duration of a transaction.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task WaitReaderAsync(ILockable lockable)
        {
            // The locking primitives don't allow for recursion.  To get around this limitation, we keep track of the locks acquired at the level of
            // a transaction and allow a lock to be acquired only once (no matter how many times this method is called), so then it can be released
            // only once.
            if (!this.readerLocks.Contains(lockable))
            {
                // Enter the lock.
                await lockable.WaitReaderAsync(this.cancellationToken);

                // Keep track of all the locks entered as we'll need to release these at the end of the transaction.
                this.readerLocks.Add(lockable);

                // If the lockable object can participate in a two-phase commit, then enlist it.
                if (lockable is IEnlistmentNotification enlistmentNotification)
                {
                    this.transaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
                }
            }
        }

        /// <summary>
        /// Asynchronously waits to write a protected resource.
        /// </summary>
        /// <param name="lockable">An object that can be locked for the duration of a transaction.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task WaitWriterAsync(ILockable lockable)
        {
            // This transaction doesn't support recursive locks. You can lock an object once, it will be released once.
            if (!this.writerLocks.Contains(lockable))
            {
                // Enter the lock.
                await lockable.WaitWriterAsync(this.cancellationToken);

                // Keep track of all the locks entered as we'll need to release these at the end of the transaction.
                this.writerLocks.Add(lockable);

                // If the lockable object can participate in a two-phase commit, then enlist it.
                if (lockable is IEnlistmentNotification enlistmentNotification)
                {
                    this.transaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
                }
            }
        }

        /// <summary>
        /// Dispose of the managed resources.
        /// </summary>
        /// <param name="disposing">An indication whether the managed resources are to be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            // This finalizes the transaction.
            this.transactionScope.Dispose();
            this.cancellationTokenSource?.Dispose();

            // Release all the locks as the last action of this transaction.
            this.readerLocks.ToList().ForEach(l => l.Release());
            this.writerLocks.ToList().ForEach(l => l.Release());
        }
    }
}