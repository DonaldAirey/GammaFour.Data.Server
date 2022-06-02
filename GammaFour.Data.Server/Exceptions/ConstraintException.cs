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
    public class ConstraintException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConstraintException"/> class.
        /// </summary>
        /// <param name="operation">the operation where the constraint violation occurred.</param>
        /// <param name="constraint">The constraint that was violated.</param>
        public ConstraintException(string operation, string constraint)
        {
            // Initialize the object.
            this.Operation = operation;
            this.Constraint = constraint;
        }

        /// <summary>
        /// Gets the constraint that was violated.
        /// </summary>
        public string Constraint { get; }

        /// <summary>
        /// Gets the operation where the constraint violation occurred.
        /// </summary>
        public string Operation { get; }
    }
}