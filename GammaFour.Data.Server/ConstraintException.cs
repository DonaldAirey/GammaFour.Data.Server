// <copyright file="ConstraintException.cs" company="Donald Roy Airey">
//    Copyright © 2022 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data.Server
{
    using System;

    /// <summary>
    /// Represents errors that occur when locking records for a transaction.
    /// </summary>
    /// <param name="operation">the operation where the constraint violation occurred.</param>
    /// <param name="constraint">The constraint that was violated.</param>
    public class ConstraintException(string operation, string constraint)
        : Exception
    {
        /// <summary>
        /// Gets the constraint that was violated.
        /// </summary>
        public string Constraint { get; } = constraint;

        /// <summary>
        /// Gets the operation where the constraint violation occurred.
        /// </summary>
        public string Operation { get; } = operation;
    }
}