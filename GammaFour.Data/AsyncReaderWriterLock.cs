// <copyright file="AsyncReaderWriterLock.cs" company="Gamma Four, Inc.">
//    Copyright © 2018 - Gamma Four, Inc.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;

    /// <summary>
    /// A lock used to manage access to a resource, allowing multiple threads for reading or exclusive access for writing.
    /// </summary>
    public class AsyncReaderWriterLock : IDisposable
    {
        /// <summary>
        /// Write locks use this to wait until the last active read lock is released.
        /// </summary>
        private readonly SemaphoreSlim readLockReleaseSemaphore = new SemaphoreSlim(0, 1);

        /// <summary>
        /// Used synchronize access to the housekeeping fields.
        /// </summary>
        private readonly object syncRoot = new object();

        /// <summary>
        /// Used to implicitly release a lock that was acquired during a transaction.
        /// </summary>
        private readonly Dictionary<Transaction, Action> transactionCompletionAction = new Dictionary<Transaction, Action>();

        /// <summary>
        /// Used to manage the write lock.
        /// </summary>
        private readonly SemaphoreSlim writeLockSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The number of currently held read locks.
        /// </summary>
        private long currentReadLockCount;

        /// <summary>
        /// The number of tasks or threads that intend to wait on the <see cref="writeLockSemaphore"/>.
        /// </summary>
        private long currentWaitingWriteLockCount;

        /// <summary>
        /// The current state of the write lock.
        /// </summary>
        private WriteLockState currentWriteLockState;

        /// <summary>
        /// Releases all resources used by the <see cref="AsyncReaderWriterLock"/>.
        /// </summary>
        public void Dispose()
        {
            // Dispose of the managed resources and supress the implicit method to clean up.
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Enters the lock in read mode.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1 to wait indefinitely.</param>
        public void EnterReadLock(int millisecondsTimeout = Timeout.Infinite)
        {
            // If the lock can't be obtained in the given time, then throw an exception.
            if (!this.TryEnterReadLockAsync(millisecondsTimeout).Result)
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Asynchronously enters the lock in read mode.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>A task that will complete when the lock has been entered.</returns>
        public async Task EnterReadLockAsync(int millisecondsTimeout = Timeout.Infinite)
        {
            // If the lock can't be obtained in the given time, then throw an exception.
            if (!await this.TryEnterReadLockAsync(millisecondsTimeout).ConfigureAwait(false))
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Tries to enter the lock in read mode, with an optional integer time-out.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1, to wait indefinitely.</param>
        /// <returns>A task that will complete with a result of trueif the lock has been entered, otherwise with a result of false.</returns>
        public bool TryEnterReadLock(int millisecondsTimeout = 0)
        {
            return this.TryEnterReadLockAsync(millisecondsTimeout).Result;
        }

        /// <summary>
        /// Tries to asynchronously enter the lock in read mode, with an optional integer time-out.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1, to wait indefinitely.</param>
        /// <returns>true if the lock has been entered, false otherwise.</returns>
        public async Task<bool> TryEnterReadLockAsync(int millisecondsTimeout = 0)
        {
            // Validate the range of the argument.
            if (millisecondsTimeout < Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
            }

            // Ensure exclusive access to the housekeeping fields.
            WriteLockState writeLockState = null;
            lock (this.syncRoot)
            {
                // The currentWriteLockState holds the housekeeping for waiting threads.  If it's empty, then no one is writing or waiting to write.
                writeLockState = this.currentWriteLockState;
                if (writeLockState == null)
                {
                    // No write lock is active, so we don't need to wait.
                    this.currentReadLockCount++;

                    // Associate the current transaction, if one exists, with an action to release this lock.
                    if (Transaction.Current != null)
                    {
                        // If we don't already have an action associated with the completion of the current transaction, then make one.
                        if (this.transactionCompletionAction.ContainsKey(Transaction.Current))
                        {
                            throw new InvalidOperationException("Attempt to enter a lock recursively.");
                        }

                        // This associates the transaction with an action to exit the read lock on completion of the transaction.
                        Transaction.Current.TransactionCompleted += this.OnTransactionCompleted;
                        this.transactionCompletionAction[Transaction.Current] = () => this.ExitReadLock();
                    }

                    // Indicates the lock was acquired and we don't have to wait.
                    return true;
                }
                else
                {
                    // If a write lock is held, then we need to wait until its semaphore is released.  But first, ensure that a semaphore exists on
                    // which we can wait.
                    if (writeLockState.WaitingReadLocksSemaphore == null)
                    {
                        writeLockState.WaitingReadLocksSemaphore = new SemaphoreSlim(0);
                    }

                    // This indicates that a read lock is waiting on a write lock.
                    writeLockState.WaitingReadLocksCount++;
                }
            }

            bool waitResult = false;
            try
            {
                // Need to wait until the existing write lock is released.  This may throw an OperationCanceledException.
                waitResult = await writeLockState.WaitingReadLocksSemaphore.WaitAsync(millisecondsTimeout)
                    .ConfigureAwait(false);
            }
            finally
            {
                // We're done waiting for a read lock.  Let's see if it was successful.
                lock (this.syncRoot)
                {
                    // Dispose of the sempahore when it's no longer needed by any tasks waiting to read.
                    writeLockState.WaitingReadLocksCount--;
                    if (writeLockState.IsReleased && writeLockState.WaitingReadLocksCount == 0)
                    {
                        writeLockState.WaitingReadLocksSemaphore.Dispose();
                    }

                    // Check to see if we acquired the lock or not.
                    if (waitResult)
                    {
                        // Read lock acquired.  Associate the current transaction, if one exists, with an action to release this lock.
                        if (Transaction.Current != null)
                        {
                            // If we don't already have an action associated with the completion of the current transaction, then make one.
                            if (this.transactionCompletionAction.ContainsKey(Transaction.Current))
                            {
                                throw new InvalidOperationException("Attempt to enter a lock recursively.");
                            }

                            // This associates the transaction with an action to exit the read lock on completion of the transaction.
                            Transaction.Current.TransactionCompleted += this.OnTransactionCompleted;
                            this.transactionCompletionAction[Transaction.Current] = () => this.ExitReadLock();
                        }
                    }
                    else
                    {
                        // Read lock not acquired.  Need to clean up housekeeping fields.
                        if (writeLockState.IsReleased)
                        {
                            this.ExitReadLockCore();
                        }
                    }
                }
            }

            // True indicates that we acquired the lock.
            return waitResult;
        }

        /// <summary>
        /// Enters the lock in write mode.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1 to wait indefinitely.</param>
        public void EnterWriteLock(int millisecondsTimeout = Timeout.Infinite)
        {
            // If we can't get a lock in the given time, then throw this exception.
            if (!this.TryEnterWriteLock(millisecondsTimeout))
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Asynchronously enters the lock in write mode.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>A task that will complete when the lock has been entered.</returns>
        public async Task EnterWriteLockAsync(int millisecondsTimeout = Timeout.Infinite)
        {
            // If we can't get a lock in the given time, then throw this exception.
            if (!await this.TryEnterWriteLockAsync(millisecondsTimeout).ConfigureAwait(false))
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Tries to enter the lock in write mode, with an optional integer time-out.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>true if the lock has been entered, otherwise, false.</returns>
        public bool TryEnterWriteLock(int millisecondsTimeout = 0)
        {
            // Execute this synchronously.
            return this.TryEnterWriteLockAsync(millisecondsTimeout).Result;
        }

        /// <summary>
        /// Tries to asynchronously enter the lock in write mode, with an optional integer time-out.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or -1 to wait indefinitely.</param>
        /// <returns>A task that will complete with a result of true if the lock has been entered, otherwise with a result of false.</returns>
        public async Task<bool> TryEnterWriteLockAsync(int millisecondsTimeout = 0)
        {
            // Validate the range of the argument.
            if (millisecondsTimeout < Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(millisecondsTimeout));
            }

            // Indicates that we obtained the lock immediately.
            bool hasLock = false;

            // This indicates that there are read locks which need to be released before we can enter the write lock.
            bool waitForReadLocks = false;

            // Ensure exclusive access to the housekeeping fields.
            lock (this.syncRoot)
            {
                // Keep track of the number of tasks waiting for a write lock on this resource.
                this.currentWaitingWriteLockCount++;

                // Check if we can immediately acquire the write lock semaphore without releasing the lock on syncroot.
                if (this.writeLockSemaphore.CurrentCount > 0 && this.writeLockSemaphore.Wait(0))
                {
                    waitForReadLocks = this.EnterWriteLockPostface(true);
                    hasLock = true;
                }
            }

            // If we didn't acquire the lock immediately, then wait for it.
            if (!hasLock)
            {
                bool writeLockWaitResult = false;
                try
                {
                    // This is where the waiting for the write lock occurs.
                    writeLockWaitResult = await this.writeLockSemaphore.WaitAsync(millisecondsTimeout)
                        .ConfigureAwait(false);
                }
                finally
                {
                    lock (this.syncRoot)
                    {
                        // Clean up the housekeeping fields.
                        this.EnterWriteLockPostface(writeLockWaitResult);

                        // Check if the write lock will need to wait for existing read locks to be released.
                        waitForReadLocks = this.currentReadLockCount > 0;
                    }
                }

                if (!writeLockWaitResult)
                {
                    return false;
                }
            }

            // After we set the write lock state, we might need to wait for existing read locks to be released.  In this state, no new read locks can
            // be entered until we release the write lock state.  We only wait one time since only the last active read lock will release the
            // semaphore.
            if (waitForReadLocks)
            {
                bool waitResult = false;
                try
                {
                    // This is where the waiting for a read lock is done.  This may throw an OperationCanceledException.
                    waitResult = await this.readLockReleaseSemaphore.WaitAsync(millisecondsTimeout)
                        .ConfigureAwait(false);
                }
                finally
                {
                    // Timeout has been exceeded.  Must clean up the housekeeping fields.
                    if (!waitResult)
                    {
                        this.HandleEnterWriteLockWaitFailure();
                    }
                }

                // Write lock timed-out waiting for reader lock to be released.
                if (!waitResult)
                {
                    return false;
                }
            }

            // We own the write lock.
            return true;
        }

        /// <summary>
        /// Exits read mode.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void ExitReadLock()
        {
            // Ensure exclusive access to the housekeeping fields.
            lock (this.syncRoot)
            {
                this.ExitReadLockCore();
            }
        }

        /// <summary>
        /// Exits write mode.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void ExitWriteLock()
        {
            // Ensure exclusive access to the housekeeping fields.
            lock (this.syncRoot)
            {
                if (this.currentWriteLockState == null)
                {
                    throw new InvalidOperationException();
                }

                this.ExitWriteLockCore();
            }
        }

        /// <summary>
        /// Releases the unmanaged resources and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true if disposing, false otherwise.</param>
        protected virtual void Dispose(bool disposing)
        {
            // These resources need to be disposed when they're not used anymore.
            if (disposing)
            {
                this.writeLockSemaphore.Dispose();
                this.readLockReleaseSemaphore.Dispose();
            }
        }

        /// <summary>
        /// Finalize an attempt to exit a read lock.
        /// </summary>
        private void ExitReadLockCore()
        {
            // We should have at least one reader when we clean up this operation.
            if (this.currentReadLockCount == 0)
            {
                throw new InvalidOperationException();
            }

            // We've got one less reader on this lock.
            this.currentReadLockCount--;

            // If we are the last read lock and there's an active write lock waiting, we need to release that task.
            if (this.currentReadLockCount == 0 && this.currentWriteLockState?.IsActive == true)
            {
                this.readLockReleaseSemaphore.Release();
            }

            // If this action is part of a transaction, then remove the implicit action to exit the read lock (because we've exited the lock
            // explicitly).
            if (Transaction.Current != null)
            {
                this.transactionCompletionAction.Remove(Transaction.Current);
                Transaction.Current.TransactionCompleted -= this.OnTransactionCompleted;
            }
        }

        /// <summary>
        /// Post-processing after a write lock has been acquired.
        /// </summary>
        /// <param name="writeLockWaitResult">The result of waiting for the write lock.</param>
        /// <returns>True indicates that tasks are waiting on this lock for reading.</returns>
        private bool EnterWriteLockPostface(bool writeLockWaitResult)
        {
            // Indicates we need to wait for read locks to be released.
            bool waitForReadLocks = false;

            // There's one less task waiting on the write lock.
            this.currentWaitingWriteLockCount--;

            // Check to see if we successfully obtained the lock.
            if (writeLockWaitResult)
            {
                // Got the lock.  If there's already a write lock state from a previous write lock, we'll reuse it.  Otherwise, create a new one.
                if (this.currentWriteLockState == null)
                {
                    this.currentWriteLockState = new WriteLockState();
                }

                // This indicates that the write lock is currently held (is active).
                this.currentWriteLockState.IsActive = true;

                // This indicates that tasks are waiting for this lock for reading.
                waitForReadLocks = this.currentReadLockCount > 0;

                // Associate the current transaction, if one exists, with an action to release this lock.
                if (Transaction.Current != null)
                {
                    // If we don't already have an action associated with the completion of the current transaction, then make one.
                    if (this.transactionCompletionAction.ContainsKey(Transaction.Current))
                    {
                        throw new InvalidOperationException("Attempt to enter a lock recursively.");
                    }

                    // This associates the transaction with an action to exit the read lock on completion of the transaction.
                    Transaction.Current.TransactionCompleted += this.OnTransactionCompleted;
                    this.transactionCompletionAction[Transaction.Current] = () => this.ExitWriteLock();
                }
            }
            else
            {
                if (this.currentWriteLockState?.IsActive == false && this.currentWaitingWriteLockCount == 0)
                {
                    // We were the last write lock and a previous (inactive) write lock state is still set, we need to release it.  This could
                    // happen e.g.  if a write lock downgrades to a read lock and then the wait on the writeLockSemaphore times out.
                    this.ReleaseWriteLockState();
                }
            }

            // Indicates to the caller that tasks are waiting to read.
            return waitForReadLocks;
        }

        /// <summary>
        /// Clean up the housekeeping fields when an attempt to acquire the write lock fails.
        /// </summary>
        private void HandleEnterWriteLockWaitFailure()
        {
            // Ensure exclusive access to the housekeeping fields.
            lock (this.syncRoot)
            {
                // Reset the read lock release semaphore if it has been released in the meanwhile.  It is OK to check this here since the semaphore
                // can only be released within the lock on syncRoot.
                if (this.readLockReleaseSemaphore.CurrentCount > 0)
                {
                    this.readLockReleaseSemaphore.Wait();
                }

                // This is the normal part of the write lock exit.
                this.ExitWriteLockCore();
            }
        }

        /// <summary>
        /// Common processing when exiting a write lock request.
        /// </summary>
        private void ExitWriteLockCore()
        {
            // If currently no other write lock is waiting, we release the current write lock state.  Otherwise, we set it to inactive so that
            // releasing the semaphore will wake up the next writer instead of a reader.
            if (this.currentWaitingWriteLockCount == 0)
            {
                this.ReleaseWriteLockState();
            }
            else
            {
                this.currentWriteLockState.IsActive = false;
            }

            // Finally, release any writers waiting on this lock.  Writes will be favored over reads.
            this.writeLockSemaphore.Release();
        }

        /// <summary>
        /// Handles the completion of a transaction.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="transactionEventArgs">The <see cref="TransactionEventArgs"/> that contains the event data.</param>
        private void OnTransactionCompleted(object sender, TransactionEventArgs transactionEventArgs)
        {
            // At the completion of a transaction, execute the completion action - which will exit a read or write lock - and remove the transaction
            // from the dictionary.
            Action action;
            lock (this.syncRoot)
            {
                action = this.transactionCompletionAction[transactionEventArgs.Transaction];
                this.transactionCompletionAction.Remove(transactionEventArgs.Transaction);
            }

            // This needs to be executed outside of the object lock.
            action();
        }

        /// <summary>
        /// Clears up the write state so that a reader will be selected next.
        /// </summary>
        private void ReleaseWriteLockState()
        {
            // Get the current state of the write lock.
            var writeLockState = this.currentWriteLockState;

            // This tells us that the semaphore used to block reading tasks is no longer being used by the write lock.
            writeLockState.IsReleased = true;

            // Check to see if there are tasks waiting to read.
            if (writeLockState.WaitingReadLocksSemaphore != null)
            {
                // If there is currently no other task or thread waiting on the semaphore, we can dispose it here.  Otherwise, the last waiting task
                // or thread must dispose the semaphore by checking the WriteLockReleased property.
                if (writeLockState.WaitingReadLocksCount == 0)
                {
                    writeLockState.WaitingReadLocksSemaphore.Dispose();
                }
                else
                {
                    // Directly mark the read locks as entered.
                    this.currentReadLockCount += writeLockState.WaitingReadLocksCount;

                    // Release the waiting read locks semaphore as often as needed to ensure all other waiting tasks or threads are released and get
                    // the read lock.  The semaphore however will only have been created if there actually was at least one other task or thread
                    // trying to get a read lock while the write lock was held.
                    writeLockState.WaitingReadLocksSemaphore.Release(writeLockState.WaitingReadLocksCount);
                }
            }

            // Clear the write lock state.
            this.currentWriteLockState = null;
        }

        /// <summary>
        /// Bookkeeping structure used to keep track of write locks.
        /// </summary>
        private class WriteLockState
        {
            /// <summary>
            /// Gets or sets a value indicating whether the state is active.  Only when true, the <see cref="readLockReleaseSemaphore"/> will be
            /// released once the last read lock exits.
            /// </summary>
            public bool IsActive { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the write lock associated with this <see cref="WriteLockState"/> has already been released.
            /// This is also used to indicate if the the task or thread that waits on the <see cref="WaitingReadLocksSemaphore"/> semaphore and then
            /// decrements <see cref="WaitingReadLocksCount"/> to zero (0) must dispose the <see cref="WaitingReadLocksSemaphore"/> semaphore.
            /// </summary>
            public bool IsReleased { get; set; }

            /// <summary>
            /// Gets or sets a <see cref="SemaphoreSlim"/> on which new read locks need to wait until the existing write lock is released.  The
            /// <see cref="SemaphoreSlim"/> will be created only if there is at least one additional task or thread that wants to enter a read lock.
            /// </summary>
            public SemaphoreSlim WaitingReadLocksSemaphore { get; set; }

            /// <summary>
            /// Gets or sets the number of tasks or threads which intend to wait on the <see cref="WaitingReadLocksSemaphore"/> semaphore.  This is
            /// used to determine which task or thread has the responsibility to dispose the <see cref="WaitingReadLocksSemaphore"/> if
            /// <see cref="IsReleased"/> is true.
            /// </summary>
            public int WaitingReadLocksCount { get; set; }
        }
    }
}