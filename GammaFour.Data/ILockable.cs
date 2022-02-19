// <copyright file="ILockable.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An object that can be locked and released.
    /// </summary>
    public interface ILockable
    {
        /// <summary>
        /// Release the lock.
        /// </summary>
        void Release();

        /// <summary>
        /// Waits for the thread to acquire a reader lock.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken to observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task WaitReaderAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Waits for the thread to enter a writer lock.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken to observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task WaitWriterAsync(CancellationToken cancellationToken);
    }
}