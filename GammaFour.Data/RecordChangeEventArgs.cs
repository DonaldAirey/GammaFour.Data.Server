// <copyright file="RecordChangeEventArgs.cs" company="Donald Roy Airey">
//    Copyright © 2020 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System;

    /// <summary>
    /// Arguments describing an event that changed a record.
    /// </summary>
    /// <typeparam name="TType">The record type.</typeparam>
    public class RecordChangeEventArgs<TType> : EventArgs
    {
        /// <summary>
        /// Gets or sets the the current version of the record.
        /// </summary>
        public TType Current { get; set; }

        /// <summary>
        /// Gets or sets the action that caused the change to the row.
        /// </summary>
        public DataAction DataAction { get; set; }

        /// <summary>
        /// Gets or sets the the previous version of the record.
        /// </summary>
        public TType Previous { get; set; }
    }
}