// <copyright file="IRow.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System.Transactions;

    /// <summary>
    /// Used by a template selector to connect a view model to the template used to display it.
    /// </summary>
    public interface IRow : IEnlistmentNotification, ILockable
    {
        /// <summary>
        /// Gets the element from the given column index.
        /// </summary>
        /// <param name="index">The column index.</param>
        /// <returns>The object in the row at the given index.</returns>
        object this[string index] { get; set; }

        /// <summary>
        /// Gets the requested version of a record.
        /// </summary>
        /// <param name="recordVersion">The record version (original, previous, current).</param>
        /// <returns>A clone of the requested version of the record.</returns>
        IRow GetVersion(RecordVersion recordVersion);
    }
}