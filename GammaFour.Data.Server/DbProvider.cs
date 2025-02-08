// <copyright file="DbProvider.cs" company="Donald Roy Airey">
//    Copyright © 2025 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    /// <summary>
    /// Codes for account types.
    /// </summary>
    public enum DbProvider
    {
        /// <summary>
        /// Microsoft SQL Server
        /// </summary>
        SqlServer,

        /// <summary>
        /// Amazon PostreSQL
        /// </summary>
        PostgreSql,

        /// <summary>
        /// MySql
        /// </summary>
        MySql,
    }
}