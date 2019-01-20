// <copyright file="DbProvider.cs" company="Gamma Four, Inc.">
//    Copyright © 2018 - Gamma Four, Inc.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
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
        MySql
    }
}