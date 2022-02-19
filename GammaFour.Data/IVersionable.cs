// <copyright file="IVersionable.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    /// <summary>
    /// Allows for the cloning of different versions of a record (original, previous, current).
    /// </summary>
    /// <typeparam name="TType">The type of record.</typeparam>
    public interface IVersionable<TType>
    {
        /// <summary>
        /// Gets the requested version of a record.
        /// </summary>
        /// <param name="recordVersion">The record version (original, previous, current).</param>
        /// <returns>A clone of the requested version of the record.</returns>
        TType GetVersion(RecordVersion recordVersion);
    }
}