// <copyright file="EnlistmentNotificationExtensions.cs" company="Donald Roy Airey">
//    Copyright © 2020 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System.Transactions;

    /// <summary>
    /// Extensions to help with enlisting in a transaction.
    /// </summary>
    public static class EnlistmentNotificationExtensions
    {
        /// <summary>
        /// Enlists a <see cref="IEnlistmentNotification"/> object in the current transaction.
        /// </summary>
        /// <param name="enlistmentNotification">The object to be enlisted in the current transaction.</param>
        public static void Enlist(this IEnlistmentNotification enlistmentNotification)
        {
            // Syntactic sugar to make the code easier to read.
            Transaction.Current.EnlistVolatile(enlistmentNotification, EnlistmentOptions.None);
        }
    }
}