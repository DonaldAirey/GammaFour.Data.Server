// <copyright file="RecordVersion.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    /// <summary>
    /// The different versions of a row.
    /// </summary>
    public enum RecordVersion
    {
        /// <summary>
        /// The current version of a row.
        /// </summary>
        Current,

        /// <summary>
        /// The original version of a row.
        /// </summary>
        Original,

        /// <summary>
        /// The previous version of a row.
        /// </summary>
        Previous,
    }
}
