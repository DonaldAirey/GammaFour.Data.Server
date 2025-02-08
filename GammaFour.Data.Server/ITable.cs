// <copyright file="ITable.cs" company="Donald Roy Airey">
//    Copyright © 2025 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Transactions;

    /// <summary>
    /// Used by a template selector to connect a view model to the template used to display it.
    /// </summary>
    public interface ITable : IEnumerable, IEnlistmentNotification
    {
        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the foreign indices.
        /// </summary>
        Dictionary<string, IForeignIndex> ForeignIndex { get; }

        /// <summary>
        /// Gets the unique indices.
        /// </summary>
        Dictionary<string, IUniqueIndex> UniqueIndex { get; }
    }
}