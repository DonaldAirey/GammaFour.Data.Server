// <copyright file="DisposableList.cs" company="Donald Roy Airey">
//    Copyright © 2020 - Donald Roy Airey.  All Rights Reserved.
// </copyright>
// <author>Donald Roy Airey</author>
namespace GammaFour.Data
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A collection of objects that will be disposed.
    /// </summary>
    public sealed class DisposableList : IDisposable
    {
        /// <summary>
        /// This is the list of items that will be disposed when this object is disposed.
        /// </summary>
        private List<IDisposable> list = new List<IDisposable>();

        /// <summary>
        /// Add a disposable element to the list.
        /// </summary>
        /// <param name="disposable">The disposable object.</param>
        public void Add(IDisposable disposable)
        {
            // Add the object to the collection that will be disposed.
            this.list.Add(disposable);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // Dispose of each of the object.
            this.list.ForEach(d => d.Dispose());
        }
    }
}
