﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace WetHatLab.OneNote.TaggingKit.common
{
    /// <summary>
    /// A observable, sorted collection of items having sortable keys.
    /// </summary>
    /// <remarks>
    /// Instances of this class provide change notification through <see cref="INotifyCollectionChanged"/>. This
    /// class is optimized for batch updates (item collections). Single items cannot be added. 
    /// </remarks>
    /// <typeparam name="Tvalue">item type providing sortable keys</typeparam>
    public class ObservableSortedList<TKey, TValue> : INotifyCollectionChanged, IEnumerable<TValue>, IDisposable
        where TValue : ISortableKeyedItem<TKey>
        where TKey   : IComparable<TKey>, IEquatable<TKey>
    {
        /// <summary>
        /// Event to notify about changes to this collection.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private SortedList<TKey, TValue> _sortedList;

        private class KeyComparer: IComparer<TKey>
        {
            #region IComparer<TKey>
            public int Compare(TKey x, TKey y)
            {
                return x.CompareTo(y);
            }
            #endregion IComparer<TKey>
        };

        public ObservableSortedList()
        {
            _sortedList = new SortedList<TKey, TValue>(new KeyComparer());
        }
        /// <summary>
        /// Get the number of items in the collection.
        /// </summary>
        public int Count
        {
            get { return _sortedList.Count; }
        }

        /// <summary>
        /// Get all items in the collection.
        /// </summary>
        public IList<TValue> Values
        {
            get { return _sortedList.Values; }
        }

        /// <summary>
        /// Determine if the list contains an item with a given key
        /// </summary>
        /// <param name="key">key to check</param>
        /// <returns>true if the given item is contained in the list; false otherwise</returns>
        public bool ContainsKey(TKey key)
        {
            return _sortedList.ContainsKey(key);
        }

        /// <summary>
        /// Clear all items from the collection.
        /// </summary>
        /// <remarks>
        /// Notifies all listeners about the change
        /// </remarks>
        public void Clear()
        {
            _sortedList.Clear();
            NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
            if (CollectionChanged != null)
            {
                CollectionChanged(this, args);
            }
        }

        /// <summary>
        /// Inform listeners to change notifications about removal of a contiguous range of items.
        /// </summary>
        /// <remarks>
        /// This method in addition also removes the items from the sorted collection. Hence it expects
        /// all given items to be still present in the sorted collection
        /// </remarks>
        /// <param name="batch">items to remove</param>
        /// <param name="startindex">start index of contiguous range of items</param>
        /// <returns>true, if batch was non empty</returns>
        private bool processRemoveBatch(LinkedList<TValue> batch, int startindex)
        {
            if (batch.Count > 0)
            {
                // remove this batch from the sorted list
                foreach (TValue dead in batch)
                {
                    bool removed = _sortedList.Remove(dead.Key);
#if DEBUG
                    Debug.Assert(removed, string.Format("Item with key '{0}' could not be removed!", dead.Key));
#endif
                }

                // fire event for this batch
                NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
                                                                                             batch.ToArray(),
                                                                                             startindex);
                if (CollectionChanged != null)
                {
                    CollectionChanged(this, args);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove items from the collection in batches. 
        /// </summary>
        /// <remarks>
        /// Groups the given items into contiguous ranges of batches and removes
        /// each batch at once, firing one change notification per batch.
        /// </remarks>
        /// <param name="items">items to remove</param>
        internal void RemoveAll(IEnumerable<TKey> keys)
        {
            SortedList<int, TValue> toDelete = new SortedList<int, TValue>();
            foreach (TKey key in keys)
            {
                int index = _sortedList.IndexOfKey(key);
                if (index >= 0)
                {
                    toDelete.Add(index, _sortedList[key]);
                }
            }

            // fire event in batches
            int n = 0;
            int batchStartIndex = -1;
            int batchLastIndex = -2;
            LinkedList<TValue> batch = new LinkedList<TValue>();
            foreach (KeyValuePair<int, TValue> item in toDelete)
            {
                if (item.Key > batchLastIndex + 1)
                {   // finish current batch
                    if (processRemoveBatch(batch, batchStartIndex - n))
                    {
                        n += batch.Count;
                        batch.Clear();
                    }

                    // ... and start new batch with this item
                    batchStartIndex = item.Key;
                    batchLastIndex = batchStartIndex - 1;
                }
#if DEBUG
                Debug.Assert(item.Key == batchLastIndex + 1);
#endif
                batchLastIndex = item.Key;
                batch.AddLast(item.Value);
            }

            // fire event for last batch
            processRemoveBatch(batch, batchStartIndex - n);
        }

        /// <summary>
        /// Inform listeners to change notifications about addition of a contiguous range of items.
        /// </summary>
        /// <remarks>
        /// It is assumed that all items in the provided batch are already present in the sorted collection.
        /// </remarks>
        /// <param name="batch">collection of items to add</param>
        /// <param name="startindex">start index of the contiguous range of items</param>
        /// <returns>true, if items were added</returns>
        private bool processAddBatch(LinkedList<TValue> batch, int startindex)
        {
#if DEBUG
            foreach (TValue itm in batch)
            {
                Debug.Assert(_sortedList.ContainsKey(itm.Key), string.Format("Item '{0}' not found in the collection!", itm.Key));
            }
#endif
            if (batch.Count > 0)
            {
                // fire event for this batch
                NotifyCollectionChangedEventArgs args = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
                                                                                             batch.ToArray(),
                                                                                             startindex);
                if (CollectionChanged != null)
                {
                    CollectionChanged(this, args);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Add items to the sorted collection in batches.
        /// </summary>
        /// <remarks>
        /// Groups the given items into contiguous ranges of batches and adds
        /// each batch at once, firing one change notification per batch.
        /// </remarks>
        /// <param name="items">items to add</param>
        internal void AddAll(IEnumerable<TValue> items)
        {
            LinkedList<TValue> addedItems = new LinkedList<TValue>();
            foreach (TValue item in items)
            {
                if (!_sortedList.ContainsKey(item.Key))
                {
                    addedItems.AddLast(item);
                    _sortedList.Add(item.Key, item);
                }
            }

            // build a sorted list of added items
            SortedList<int, TValue> sortedAdds = new SortedList<int, TValue>();
            foreach (TValue a in addedItems)
            {
                int index = _sortedList.IndexOfKey(a.Key);
#if DEBUG
                Debug.Assert(index >= 0, string.Format("previously added item not found: {0}", a.Key));
#endif
                sortedAdds.Add(index, a);
            }

            // fire event in batches
            int batchStartIndex = -1;
            int batchLastIndex = -2;
            addedItems.Clear();

            foreach (KeyValuePair<int, TValue> item in sortedAdds)
            {
                if (item.Key > batchLastIndex + 1)
                {
                    // process current batch
                    if (processAddBatch(addedItems, batchStartIndex))
                    {
                        addedItems.Clear();
                    }
                    // ... and start a new batch with this item
                    batchStartIndex = item.Key;
                    batchLastIndex = batchStartIndex - 1;
                }
#if DEBUG
                Debug.Assert(item.Key == batchLastIndex + 1);
#endif
                batchLastIndex = item.Key;
                addedItems.AddLast(item.Value);
            }

            // fire event for last batch
            processAddBatch(addedItems, batchStartIndex);
        }
        #region IDisposable
        public void Dispose()
        {
            CollectionChanged = null;
        }
        #endregion

        #region IEnumeable<TValue>
        public IEnumerator<TValue> GetEnumerator()
        {
            return _sortedList.Values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _sortedList.Values.GetEnumerator();
        }

        #endregion IEnumeable<TValue>

    }
}
