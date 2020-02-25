// <copyright file="DataAction.cs" company="Donald Roy Airey">
//    Copyright © 2020 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    /// <summary>
    /// An action on a data model.
    /// </summary>
    public enum DataAction
    {
        /// <summary>
        /// Add an item.
        /// </summary>
        Add,

        /// <summary>
        /// Delete an item.
        /// </summary>
        Delete,

        /// <summary>
        /// Rollback an item.
        /// </summary>
        Rollback,

        /// <summary>
        /// Update an item.
        /// </summary>
        Update,
    }
}
