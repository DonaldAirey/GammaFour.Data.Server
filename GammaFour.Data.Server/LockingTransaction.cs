// <copyright file="LockingTransaction.cs" company="Donald Roy Airey">
//    Copyright © 2025 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using DotNext.Threading;

    /// <summary>
    /// An extension to the <see cref="TransactionScope"/> that also handles resource locking.
    /// </summary>
    public class LockingTransaction : IDisposable
    {
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
        private readonly List<AsyncLock.Holder> holders = new List<AsyncLock.Holder>();

        /// <summary>
        /// A cancellation token provided by the caller.
        /// </summary>
        private readonly CancellationToken cancellationToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="LockingTransaction"/> class.
        /// </summary>
        /// <param name="transactionTimeout">The TimeSpan after which the transaction scope times out and aborts the transaction.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public LockingTransaction(TimeSpan transactionTimeout = default, CancellationToken cancellationToken = default)
        {
            // Provide a default cancellation token if not provided by the caller.
            this.cancellationToken = cancellationToken == default ? CancellationToken.None : cancellationToken;

            // Initialize the object.
            this.transactionScope = new TransactionScope(
                TransactionScopeOption.RequiresNew,
                transactionTimeout == default ? TransactionManager.DefaultTimeout : transactionTimeout,
                TransactionScopeAsyncFlowOption.Enabled);
            ArgumentNullException.ThrowIfNull(Transaction.Current);
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
            // Dispose of the object.
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously waits to read a protected resource.
        /// </summary>
        /// <param name="enlistmentNotification">An object that can be enlisted into a transaction.</param>
        public void Add(IEnlistmentNotification enlistmentNotification)
        {
            // Add the enlistable to the transaction.
            this.transaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
        }

        /// <summary>
        /// Asynchronously waits to read a protected resource.
        /// </summary>
        /// <param name="enlistmentNotification">An object that can be enlisted into a transaction.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task WaitReaderAsync(IEnlistmentNotification enlistmentNotification)
        {
            // Lock the object for reading and add it to the transaction.
            this.holders.Add(await enlistmentNotification.AcquireReadLockAsync(this.cancellationToken));
            this.transaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
        }

        /// <summary>
        /// Asynchronously waits to write a protected resource.
        /// </summary>
        /// <param name="enlistmentNotification">An object that can be enlisted into a transaction.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        public async Task WaitWriterAsync(IEnlistmentNotification enlistmentNotification)
        {
            // Lock the object for writing and add it to the transaction.
            this.holders.Add(await enlistmentNotification.AcquireReadLockAsync(this.cancellationToken));
            this.transaction.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
        }

        /// <summary>
        /// Dispose of the managed resources.
        /// </summary>
        /// <param name="disposing">An indication whether the managed resources are to be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            // Dispose of the managed objects.
            if (disposing)
            {
                foreach (var holder in this.holders)
                {
                    holder.Dispose();
                }
            }
        }
    }
}