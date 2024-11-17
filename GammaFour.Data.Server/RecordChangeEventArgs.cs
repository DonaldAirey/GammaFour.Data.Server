// <copyright file="RecordChangeEventArgs.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;

    /// <summary>
    /// Arguments describing an event that changed a record.
    /// </summary>
    /// <typeparam name="T">The key type.</typeparam>
    /// <remarks>
    /// Initializes a new instance of the <see cref="RecordChangeEventArgs{TType}"/> class.
    /// </remarks>
    /// <param name="dataAction">The action which changed the record.</param>
    /// <param name="previous">The previous record.</param>
    /// <param name="current">The current record.</param>
    public class RecordChangeEventArgs<T>(DataAction dataAction, T? previous, T? current)
        : EventArgs
        where T : IRow
    {
        /// <summary>
        /// Gets or sets the the current version of the record.
        /// </summary>
        public T? Current { get; set; } = current;

        /// <summary>
        /// Gets or sets the action that caused the change to the row.
        /// </summary>
        public DataAction DataAction { get; set; } = dataAction;

        /// <summary>
        /// Gets or sets the the previous version of the record.
        /// </summary>
        public T? Previous { get; set; } = previous;
    }
}