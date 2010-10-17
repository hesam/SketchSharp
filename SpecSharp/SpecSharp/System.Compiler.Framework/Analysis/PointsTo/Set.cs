//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
/// The definition of the ISet interface, and one implementation. 

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace System.Collections.Generic {
  public interface ISet<T> : ICollection<T>, IEnumerable<T> {
    /// <summary>
    /// Add all the elements in the <code>range</code> to this set
    /// </summary>
    void AddRange(IEnumerable<T> range);

    /// <summary>
    /// Convert all the elements in the set
    /// </summary>
    ISet<U> ConvertAll<U>(Converter<T, U> converter);

    /// <summary>
    /// Set difference (this \ b).
    /// We want the result to be a fresh set
    /// </summary>
    ISet<T> Difference(IEnumerable<T> b);

    /// <summary>
    /// Return the subset of elements in this set that satisfy the predicate
    /// </summary>
    ISet<T> FindAll(Predicate<T> predicate);

    /// <summary>
    /// Apply the <code>action</code> to all the elements of the set
    /// </summary>
    void ForEach(Action<T> action);

    /// <summary>
    /// Does it exist an element in the set that satisfies <code>predicate</code>
    /// </summary>
    bool Exists(Predicate<T> predicate);

    /// <summary>
    /// Set intersection.
    /// We want the result to be a fresh set
    /// </summary>
    ISet<T> Intersection(ISet<T> b);

    /// <summary>
    /// True iff this is a subset of <code>s</code>
    /// </summary>
    bool IsSubset(ISet<T> s);

    ///<summary>
    /// True iff the set is empty
    ///</summary>
    bool IsEmpty { get; }

    /// <summary>
    /// True iff the set contains just one element
    /// </summary>
    bool IsSingleton { get; }

    /// <returns>
    /// An element of the set.
    /// It is not removed!!!
    /// </returns>
    T PickAnElement();

    /// <summary>
    ///  Check if the predicate holds for all the elements in the set
    /// </summary>
    bool TrueForAll(Predicate<T> predicate);

    /// <summary>
    /// Set union.
    /// We want the result to be a fresh set
    /// </summary>
    ISet<T> Union(ISet<T> b);
  }

  /// <summary>
  /// An implementation of ISet based on the BCL class Dictionary
  /// </summary>
  /// <typeparam name="T">The type of the elements of the set</typeparam>
  [Serializable]
  public class Set<T> : ISet<T> {
    #region Private Fields
    private Dictionary<T, bool> data;
    #endregion

    #region Constructors
    /// <summary>
    /// Create an empty set of standard capacity
    /// </summary>
    public Set() {
      this.data = new Dictionary<T, bool>();
    }

    /// <summary>
    /// Create a set of initial <code>capacity</code>
    /// </summary>
    public Set(int capacity) {
      this.data = new Dictionary<T, bool>(capacity);
    }

    /// <summary>
    /// Create a singleton
    /// </summary>
    public Set(T singleton) {
      this.data = new Dictionary<T, bool>();
      this.Add(singleton);
    }

    /// <summary>
    /// Create a set containing the same elements than <code>original</code>
    /// </summary>
    public Set(Set<T> original) {
      this.data = new Dictionary<T, bool>(original.data);
    }

    /// <summary>
    /// Create a set containing the same elements than <code>original</code>
    /// </summary>
    public Set(IEnumerable<T> original) {
      this.data = new Dictionary<T, bool>();
      AddRange(original);
    }
    #endregion

    public int Count {
      get {
        return this.data.Count;
      }
    }

    public bool IsEmpty {
      get {
        return this.Count == 0;
      }
    }

    public bool IsSingleton {
      get {
        return this.Count == 1;
      }
    }

    /// <summary>
    /// Add an element to the set. 
    /// If it already exists, it does nothing
    /// </summary>
    /// <returns>true if element was NOT previously present</returns>
    public bool AddQ(T a) {
      if (!this.data.ContainsKey(a)) {
        this.data.Add(a, true);
        return true;
      }
      return false;
    }

    public void Add(T a) {
      this.data[a] = true;
    }

    /// <summary>
    /// Add all the elements in the <code>range</code> to this set
    /// </summary>
    public void AddRange(IEnumerable<T> range) {
      foreach (T a in range)
        Add(a);
    }

    /// <summary>
    /// Convert all the elements of the set
    /// </summary>
    public Set<U> ConvertAll<U>(Converter<T, U> converter) {
      Set<U> result = new Set<U>(this.Count);
      foreach (T element in this) {
        result.Add(converter(element));
      }
      return result;
    }

    /// <summary>
    ///  Check if the predicate holds for all the elements in the set
    /// </summary>
    public bool TrueForAll(Predicate<T> predicate) {
      foreach (T element in this)
        if (!predicate(element))
          return false;
      return true;
    }

    /// <summary>
    /// Return the subset of elements in this set that satisfy the predicate
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public Set<T> FindAll(Predicate<T> predicate) {
      var result = new Set<T>();
      foreach (T element in this)
        if (predicate(element))
          result.Add(element);
      return result;
    }

    /// <summary>
    /// Does it exist an element in the set that satisfies <code>predicate</code>
    /// </summary>
    public bool Exists(Predicate<T> predicate) {
      foreach (T element in this) {
        if (predicate(element))
          return true;
      }

      return false;
    }

    /// <summary>
    /// Apply the <code>action</code> to all the elements of the set
    /// </summary>
    public void ForEach(Action<T> action) {
      foreach (T element in this)
        action(element);
    }

    /// <returns>
    /// An element of the set.
    /// It is not removed;
    /// </returns>
    public T PickAnElement() {
      IEnumerator<T> e = this.data.Keys.GetEnumerator();
      e.MoveNext();
      return e.Current;
    }

    /// <summary>
    /// Remove all the elements from this set.
    /// </summary>
    public void Clear() {
      data.Clear();
    }

    /// <summary>
    /// True iff <code>a</code> is in the set
    /// </summary>
    public bool Contains(T a) {
      return data.ContainsKey(a);
    }

    /// <summary>
    /// True iff this is a subset of <code>s</code>
    /// </summary>
    public bool IsSubset(Set<T> s) {
      if (this.Count > s.Count) {
        return false;
      }

      foreach (T e in this) {
        if (!s.Contains(e)) {
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Remove the element <code>a</code> from the set
    /// </summary>
    /// <param name="a"></param>
    /// <returns></returns>
    public bool Remove(T a) {
      return this.data.Remove(a);
    }

    //^ [Pure]
    public IEnumerator<T>/*!*/ GetEnumerator() {
      return data.Keys.GetEnumerator();
    }

    public bool IsReadOnly {
      get {
        return false;
      }
    }

    /// <summary>
    /// Set union in infix form
    /// </summary>
    public static Set<T> operator |(Set<T> a, Set<T> b) {
      var result = new Set<T>(a);
      result.AddRange(b);

      return result;
    }

    /// <summary>
    /// Set union in 
    /// </summary>
    public Set<T> Union(Set<T> b) {
      if (this.Count == 0)
        return b;

      if (b.Count == 0)
        return this;

      Set<T> asSet = b as Set<T>;

      if (asSet == null)
        asSet = new Set<T>(b);

      return this | asSet;
    }

    /// <summary>
    /// Set intersection in infix form
    /// </summary>
    public static Set<T> operator &(Set<T> a, Set<T> b) {
      Set<T> result = new Set<T>();
      foreach (T element in a) {
        if (b.Contains(element))
          result.Add(element);
      }
      return result;
    }

    /// <summary>
    /// Set intersection 
    /// </summary>
    public Set<T> Intersection(Set<T> b) {
      if (b.Count == 0)
        return b;

      if (this.Count == 0)
        return this;

      Set<T> asSet = b as Set<T>;

      if (asSet == null)
        asSet = new Set<T>(b);

      return this & asSet;
    }

    /// <summary>
    /// Set difference in infix form
    /// </summary>
    public static Set<T> operator -(Set<T> a, Set<T> b) {
      Set<T> result = new Set<T>();
      foreach (T element in a)
        if (!b.Contains(element))
          result.Add(element);
      return result;
    }

    /// <summary>
    /// Set difference
    /// </summary>
    public Set<T> Difference(IEnumerable<T> b) {
      return this - new Set<T>(b);
    }

    #region Unused methods on sets

    public static Set<T> operator ^(Set<T> a, Set<T> b) {
      Set<T> result = new Set<T>();
      foreach (T element in a)
        if (!b.Contains(element))
          result.Add(element);
      foreach (T element in b)
        if (!a.Contains(element))
          result.Add(element);
      return result;
    }
    public Set<T> SymmetricDifference(IEnumerable<T> b) {
      return this ^ new Set<T>(b);
    }

    public static Set<T> Empty {
      get {
        return new Set<T>(0);
      }
    }

    public static bool operator <=(Set<T> a, Set<T> b) {
      foreach (T element in a)
        if (!b.Contains(element))
          return false;
      return true;
    }
    public static bool operator <(Set<T> a, Set<T> b) {
      return (a.Count < b.Count) && (a <= b);
    }

    public static bool operator >(Set<T> a, Set<T> b) {
      return b < a;
    }
    public static bool operator >=(Set<T> a, Set<T> b) {
      return (b <= a);
    }

    //^ [StateIndependent]
    public override bool Equals(object/*?*/ obj) {
      Set<T> a = this;
      Set<T>/*?*/ b = obj as Set<T>;
      if (Object.Equals(b, null))
        return false;
      return a == b;
    }

    //^ [Confined]
    public override int GetHashCode() {
      int hashcode = 0;
      foreach (T element in this) {
        Debug.Assert(element != null, "I was not expecting a null element in the set...");
        //^ assert element != null;

        hashcode ^= element.GetHashCode();
      }
      return hashcode;
    }

    #endregion

    //^ [Pure]
    IEnumerator/*!*/ IEnumerable.GetEnumerator() {
      return ((IEnumerable)data.Keys).GetEnumerator();
    }

    public void CopyTo(T[]/*!*/ array, int index) {
      this.data.Keys.CopyTo(array, index);
    }

    //^ [Confined]
    override public string/*!*/ ToString() {
#if DEBUG
      string res = "{";

      foreach (T e in this) {
        res += (e != null ? e.ToString() : "<null>") + " ,";
      }
      if (res[res.Length - 1] == ',')
        res = res.Substring(0, res.Length - 2);

      res += "}";
#else
      string res = this.Count.ToString() + " elements";
#endif
      return res;
    }

    #region ISet<T> Members

    void ISet<T>.AddRange(IEnumerable<T> range) {
      this.AddRange(range);
    }

    ISet<U> ISet<T>.ConvertAll<U>(Converter<T, U> converter) {
      return this.ConvertAll(converter);
    }

    ISet<T> ISet<T>.Difference(IEnumerable<T> b) {
      return this.Difference(b);
    }

    ISet<T> ISet<T>.FindAll(Predicate<T> predicate) {
      return this.FindAll(predicate);
    }

    void ISet<T>.ForEach(Action<T> action) {
      this.ForEach(action);
    }

    bool ISet<T>.Exists(Predicate<T> predicate) {
      return this.Exists(predicate);
    }

    ISet<T> ISet<T>.Intersection(ISet<T> b) {
      Set<T> bAsSet = b as Set<T>;
      if (bAsSet != null)
        return this.Intersection(bAsSet);
      else
        return this.Intersection(new Set<T>(b));
    }

    bool ISet<T>.IsSubset(ISet<T> s) {
      Set<T> sAsSet = s as Set<T>;
      if (sAsSet != null)
        return this.IsSubset(sAsSet);
      else
        return this.IsSubset(new Set<T>(s));
    }

    bool ISet<T>.IsEmpty {
      get { return this.IsEmpty; }
    }

    bool ISet<T>.IsSingleton {
      get { return this.IsSingleton; }
    }

    T ISet<T>.PickAnElement() {
      return this.PickAnElement();
    }

    bool ISet<T>.TrueForAll(Predicate<T> predicate) {
      return this.TrueForAll(predicate);
    }

    ISet<T> ISet<T>.Union(ISet<T> b) {
      Set<T> bAsSet = b as Set<T>;
      if (bAsSet != null)
        return this.Union(bAsSet);
      else
        return this.Union(new Set<T>(b));
    }
    #endregion

  }

}
