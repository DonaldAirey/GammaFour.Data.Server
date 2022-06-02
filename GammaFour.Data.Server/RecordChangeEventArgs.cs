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
    public class RecordChangeEventArgs<T> : EventArgs
        where T : IRow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecordChangeEventArgs{TType}"/> class.
        /// </summary>
        /// <param name="dataAction">The action which changed the record.</param>
        /// <param name="previous">The previous record.</param>
        /// <param name="current">The current record.</param>
        public RecordChangeEventArgs(DataAction dataAction, T previous, T current)
        {
            // Initialize the object.
            this.Current = current;
            this.DataAction = dataAction;
            this.Previous = previous;
        }

        /// <summary>
        /// Gets or sets the the current version of the record.
        /// </summary>
        public T Current { get; set; }

        /// <summary>
        /// Gets or sets the action that caused the change to the row.
        /// </summary>
        public DataAction DataAction { get; set; }

        /// <summary>
        /// Gets or sets the the previous version of the record.
        /// </summary>
        public T Previous { get; set; }
    }
}