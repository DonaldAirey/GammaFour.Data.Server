// <copyright file="LockingTransaction.cs" company="Donald Roy Airey">
//    Copyright © 2021 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
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
        private readonly CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// The transaction.
        /// </summary>
        private readonly Transaction transaction;

        /// <summary>
        /// Provides a transactional code block.
        /// </summary>
        private readonly TransactionScope transactionScope;

        /// <summary>
        /// Collection of locks for the resources used by the transactional code block.
        /// </summary>
        private readonly HashSet<ILockable> locks = new HashSet<ILockable>();

        /// <summary>
        /// A cancellation token provided by the caller.
        /// </summary>
        private readonly CancellationToken cancellationToken = default;

        /// <summary>
        /// Initializes a new instance of the <see cref="LockingTransaction"/> class.
        /// </summary>
        /// <param name="transactionTimeout">The TimeSpan after which the transaction scope times out and aborts the transaction.</param>
        /// <param name="cancellationToken">Used to cancel the entire transaction.</param>
        public LockingTransaction(TimeSpan transactionTimeout = default, CancellationToken cancellationToken = default)
        {
            // If no transaction timeout is supplied, then the transactions will wait forever for a resource.
            transactionTimeout = transactionTimeout == default ? Timeout.InfiniteTimeSpan : transactionTimeout;

            // Initialize the object.
            this.transactionScope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionTimeout, TransactionScopeAsyncFlowOption.Enabled);
            this.transaction = Transaction.Current;

            // The cancellation token here is used to kill attempts to acquire a lock during this transaction.  If a cancellation hasn't been
            // provided, then implicitly create one using the same timeout value used for the transaction.
            if (cancellationToken == default)
            {
                // Create an implicit cancellation token based on a timeout.
                this.cancellationTokenSource = new CancellationTokenSource(transactionTimeout);
                this.cancellationToken = this.cancellationTokenSource.Token;
            }
            else
            {
                // Use the cancellation token provided by the caller.
                this.cancellationToken = cancellationToken;
            }
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
        /// Asynchronously waits to enter the transaction.
        /// </summary>
        /// <param name="lockable">An object that can be locked for the duration of a transaction.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task WaitAsync(ILockable lockable)
        {
            // This transaction doesn't support recursive locks. You can lock an object once, it will be released once.
            if (!this.locks.Contains(lockable))
            {
                // Enter the lock.
                await lockable.WaitAsync(this.cancellationToken);

                // Keep track of all the locks entered as we'll need to release these at the end of the transaction.
                this.locks.Add(lockable);

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
            if (this.cancellationTokenSource != null)
            {
                this.cancellationTokenSource.Dispose();
            }

            // Release all the locks as the last action of this transaction.
            this.locks.ToList().ForEach(l => l.Release());
        }
    }
}