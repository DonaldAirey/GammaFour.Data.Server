// <copyright file="IMergable.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System.Collections.Generic;

    /// <summary>
    /// Used by a template selector to connect a view model to the template used to display it.
    /// </summary>
    public interface IMergable
    {
        /// <summary>
        /// A method to merge a record into a set.
        /// </summary>
        /// <param name="source">A set of records.</param>
        /// <returns>The records that couldn't be merged.</returns>
        IEnumerable<object> Merge(IEnumerable<object> source);
    }
}