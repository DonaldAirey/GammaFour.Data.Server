// <copyright file="ILockable.cs" company="Donald Roy Airey">
//    Copyright © 2021 - Donald Roy Airey.  All Rights Reserved.
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
        /// Waits for the thread to enter a lock.
        /// </summary>
        /// <param name="cancellationToken">The CancellationToken to observe.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task WaitAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Release the lock.
        /// </summary>
        void Release();
    }
}