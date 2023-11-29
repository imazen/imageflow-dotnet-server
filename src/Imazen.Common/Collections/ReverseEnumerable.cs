// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using System.Collections;

namespace Imazen.Common.Collections {
    /// <summary>
    /// Enumerates the collection backwards
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReverseEnumerable<T>:IEnumerable<T> {
        private readonly ReadOnlyCollection<T> collection;
        public ReverseEnumerable(ReadOnlyCollection<T> collection){
            this.collection = collection;
        }
        public IEnumerator<T> GetEnumerator() {
            return new ReverseEnumerator<T>(collection);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return new ReverseEnumerator<T>(collection);
        }
    }
    /// <summary>
    /// Enumerates the collection from end to beginning
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class ReverseEnumerator<T> : IEnumerator<T>
    {
        private readonly ReadOnlyCollection<T> collection;
        private int currentIndex;

        public ReverseEnumerator(ReadOnlyCollection<T> collection)
        {
            this.collection = collection;
            currentIndex = collection.Count; // Start just after the last element
        }

        public T Current
        {
            get
            {
                if (currentIndex < 0 || currentIndex >= collection.Count)
                {
                    throw new InvalidOperationException("The collection was modified after the enumerator was created.");
                }
                return collection[currentIndex];
            }
        }

        object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            currentIndex--;
            return currentIndex >= 0;
        }

        public void Reset()
        {
            currentIndex = collection.Count;
        }

        public void Dispose()
        {
            // No resources to dispose in this example
        }
    }

}