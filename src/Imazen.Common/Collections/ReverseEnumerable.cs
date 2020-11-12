// Copyright (c) Imazen LLC.
// No part of this project, including this file, may be copied, modified,
// propagated, or distributed except as permitted in COPYRIGHT.txt.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;

namespace Imazen.Common.Collections {
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
    public class ReverseEnumerator<T> : IEnumerator<T> {
        private readonly ReadOnlyCollection<T> collection;
        private int curIndex;


        public ReverseEnumerator(ReadOnlyCollection<T> collection) {
            this.collection = collection;
            curIndex = this.collection.Count;
            Current = default;

        }

        public bool MoveNext() {
            curIndex--;
            //Avoids going beyond the beginning of the collection.
            if (curIndex < 0) {
                Current = default;
                return false;
            }

            // Set current box to next item in collection.
            Current = collection[curIndex];
            return true;
        }

        public void Reset() { curIndex = collection.Count; Current = default(T); }

        void IDisposable.Dispose() { }

        public T Current { get; private set; }


        object IEnumerator.Current => Current;
    }
}