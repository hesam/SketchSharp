// Generic typesafe collections in Generic C#
// This program requires .Net version 2.0.
// Peter Sestoft (sestoft@dina.kvl.dk) 2001-12-02, 2003-11-23, 2004-07-26

// See documentation in file collections.txt

// To create a module for use from other files, compile with 
//   csc /t:module GCollections.cs

// To do:
//  * Test systematically (no time, unfortunately)
//  * Make GetHashCode constant time everywhere
//  * Make the collections themselves implement IComparable<T> ?
//  * Add interval operators to ISortedSet and ISortedMap operations
//  * Implement HashSets from the ground up, using linked list for
//    buckets, and retaining the hashcode to avoid recomputing it.
//  * There's a fair amount of code duplication (generic/non-generic)
//    in TreeMap, but I fear the performance consequences of removing it.

using System;			// For exceptions

namespace GCollections {

// INTERFACES ===================================================

// Enumerators --------------------------------------------------

public interface IEnumerator<T> {
  T Current { get; } 
  bool MoveNext();
  void Reset();
}

// Enumerables --------------------------------------------------

public interface IEnumerable<T> { 
  IEnumerator<T> GetEnumerator();
}

// Collections --------------------------------------------------

public interface ICollection<T> : IEnumerable<T> { 
  int Count { get; }
}

// Comparing two things -----------------------------------------

public interface IComparer<T> {
  int Compare(T v1, T v2);
}

// Comparing to type T ------------------------------------------

public interface IComparable<T> {
  int CompareTo(T that);
}

// Maps ---------------------------------------------------------

public interface IMap<K,V> : ICollection< MapEntry<K,V> > {
  bool Add(K key, V val);       // Cannot return old value ...
  MapEntry<K,V> Remove(K key);
  V this[K key] { get; set; }
  bool Contains(K key);
  //  ICollection<K> Keys { get; }
  //  ICollection<V> Values { get; }
}

// Sorted maps --------------------------------------------------

public interface ISortedMap<K, V> : IMap<K, V> { }

// Map entries --------------------------------------------------

public struct MapEntry<K,V> {
  K key; V val;
  
  public MapEntry(K key, V val) {
    this.key = key; this.val = val;
  }

  public K Key { get { return key; } }

  public V Value { get { return val; } }
}

// Sets ---------------------------------------------------------

public interface ISet<T> : ICollection<T> {
  bool Add(T item);             // return true if item was added
  T Remove(T item);             // return removed item
  bool Contains(T item);
}

// Sorted sets --------------------------------------------------

public interface ISortedSet<T> : ISet<T> { }

// Lists, stacks and queues -------------------------------------

public interface IList<T> : ICollection<T> {
  bool Add(T item);
  bool Add(int i, T item);
  T Remove();
  T RemoveAt(int i);
  T Remove(T item);
  bool Contains(T item);        // using Equals
  T this[int index] { get; set; }
}

// IMPLEMENTATIONS ==============================================

// HashMaps -----------------------------------------------------

public class HashMap<K,V> : IMap<K,V> {
  private System.Collections.Hashtable table;
  
  public HashMap() {
    table = new System.Collections.Hashtable();
  }

  public ICollection<K> Keys {
    get { return new Collection<K>(table.Keys); }
  }

  public ICollection<V> Values {
    get { return new Collection<V>(table.Values); }
  }

  public int Count {
    get { return table.Count; }
  }

  public bool Add(K key, V val) {
    if (table.Contains(key))
      return false;
    else { 
      table.Add(key, val);
      return true;
    }
  }

  public MapEntry<K,V> Remove(K key) {
    if (table.Contains(key)) {
      V val = (V)table[key];
      table.Remove(key);
      return new MapEntry<K,V>(key, val); // Issue: not quite as spec'ed
    } else
      throw new ElementNotFoundException();
  }
  
  public V this[K key] { 
    get { return (V)table[key]; }
    set { table[key] = value; }
  }
  
  public bool Contains(K key) {
    return table.Contains(key);
  }

  // Two classes to help convert the underlying Hashtable's Keys and
  // Values object ICollections into generic collections

  class Collection<T> : ICollection<T> {
    private System.Collections.ICollection coll;
    
    public Collection(System.Collections.ICollection coll) {
      this.coll = coll;
    }
    
    public IEnumerator<T> GetEnumerator() {
      return new Enumerator<T>(coll.GetEnumerator());
    }

    public int Count {
      get { return coll.Count; }
    }
  }

  // Issue: this function could be optimized to not use the enumerators

  public override int GetHashCode() {
    int sum = 0;
    foreach (MapEntry<K,V> entry in this)
      sum += entry.Key.GetHashCode() ^ entry.Value.GetHashCode();
    return sum;
  }
  
  class Enumerator<T> : IEnumerator<T> {
    System.Collections.IEnumerator enm;
    
    public Enumerator(System.Collections.IEnumerator enm) {
      this.enm = enm;
    }
    
    public T Current { 
      get { return (T)enm.Current; }
    }

    public bool MoveNext() {
      return enm.MoveNext();
    }

    public void Reset() {
      enm.Reset();
    }
  }

  public IEnumerator<MapEntry<K,V>> GetEnumerator() {
    return new HashMapEnumerator(this, table.GetEnumerator());
  }

  class HashMapEnumerator : IEnumerator<MapEntry<K,V>> {
    HashMap<K,V> map;
    System.Collections.IEnumerator enm;
    
    public HashMapEnumerator(HashMap<K,V> map, 
                             System.Collections.IEnumerator enm) {
      this.map = map; this.enm = enm;
    }

    // Issue: should probably not create a new struct on every invocation
    public virtual MapEntry<K,V> Current { 
      get { 
        System.Collections.DictionaryEntry entry 
          = (System.Collections.DictionaryEntry)enm.Current; 
        return new MapEntry<K,V>((K)entry.Key, (V)entry.Value);
      }
    }

    public bool MoveNext() {
      return enm.MoveNext();
    }
    
    public void Reset() {
      enm.Reset();
    }
  }
}

// HashSet, sets of unordered items -----------------------------

public class HashSet<T> : ISet<T> {
  private System.Collections.Hashtable /* from T to null */ table;
  private int hashCode = 0;     // to save recomputing it
  
  public HashSet() { 
    table = new System.Collections.Hashtable();
  }

  public HashSet(T v) : this() { 
    Add(v);
  }
  
  public HashSet(HashSet<T> s) : this() { 
    IEnumerator<T> sIter = s.GetEnumerator();
    while (sIter.MoveNext())
      Add(sIter.Current);
  }
  
  public int Count { 
    get { return table.Count; }
  }

  public bool Add(T v) {
    if (!table.Contains(v)) {
      table.Add(v, null);
      hashCode += v.GetHashCode();
      return true;
    } else
      return false;
  }

  public T Remove(T v) {
    if (table.Contains(v)) {
      table.Remove(v);
      hashCode -= v.GetHashCode();
      return v;				// Issue: not quite according to spec
    } else
      throw new ElementNotFoundException();      
  }

  public bool Contains(T v) {
    return table.Contains(v);
  }

  public IEnumerator<T> GetEnumerator() {
    return new HashSetEnumerator(table);
  }

  class HashSetEnumerator : IEnumerator<T> {
    System.Collections.IEnumerator enm;
    
    public HashSetEnumerator(System.Collections.Hashtable table) {
      this.enm = table.Keys.GetEnumerator();
    }

    public virtual T Current { 
      get { return (T)enm.Current; }
    }

    public bool MoveNext() {
      return enm.MoveNext();
    }
    
    public void Reset() {
      enm.Reset();
    }
  }
  
  public override int GetHashCode() {
    return hashCode;
  }

  public override bool Equals(object that) {
    // Issue: could compare hashcodes, if GetHashCode were constant time
    if (that is ISet<T> && Count == ((ISet<T>)that).Count) {
      ISet<T> thatSet = (ISet<T>)that;
      IEnumerator<T> thisenm = this.GetEnumerator();
      while (thisenm.MoveNext()) {
        if (!thatSet.Contains(thisenm.Current)) 
          return false;
      }
      return true;
    } else
      return false;
  }
}

// Doubly-linked lists ------------------------------------------
// Add(T) at end, Remove() from front; behaves like a queue (FIFO)

public class LinkedList<T> : IList<T> {
  int size;			// Number of elements in the list
  int stamp;			// To detect modification during enumeration
  Node first, last;		// Invariant: first==null iff last==null

  private class Node {
    public Node prev, next;
    public T item;

    public Node(T item) {
      this.item = item; 
    }

    public Node(T item, Node prev, Node next) {
      this.item = item; this.prev = prev; this.next = next; 
    }
  }

  public LinkedList() {
    first = last = null;
    size = stamp = 0;
  }

  public int Count {
    get { return size; }
  }

  public T this[int index] {
    get { return get(index).item; }
    set { get(index).item = value; }
  }      

  private Node get(int n) {
    if (n < 0 || n >= size)
      throw new IndexOutOfRangeException();
    else if (n < size/2) {              // Closer to front
      Node node = first;
      for (int i=0; i<n; i++)
        node = node.next;
      return node;
    } else {                            // Closer to end
      Node node = last;
      for (int i=size-1; i>n; i--)
        node = node.prev;
      return node;
    }
  }

  public bool Add(T item) { 
    return AddLast(item);
  }

  public bool AddFirst(T item) { 
    if (first == null) // and thus last == null
      first = last = new Node(item);
    else {
      Node tmp = new Node(item, null, first);
      first.prev = tmp;
      first = tmp;
    }
    size++;
    stamp++;
    return true;
  }

  public bool Add(int i, T item) { 
    if (i == 0) 
      return AddFirst(item);
    else if (i == size)
      return AddLast(item);
    else {
      Node node = get(i);
      // assert node.prev != null;
      Node newnode = new Node(item, node.prev, node);
      node.prev.next = newnode;
      node.prev = newnode;
      size++;
      stamp++;
      return true;
    }
  }

  public bool AddLast(T item) {
    if (last == null) // and thus first = null
      first = last = new Node(item);
    else {
      Node tmp = new Node(item, last, null);
      last.next = tmp;
      last = tmp;
    }
    size++; 
    stamp++;
    return true;
  }

  public T Remove() {
    return RemoveFirst();
  }

  public T RemoveFirst() {
    if (first == null) // and thus last == null
      throw new IndexOutOfRangeException();
    else {
      size--; 
      stamp++;
      T item = first.item;
      first = first.next;
      if (first == null) 
        last = null;
      else 
        first.prev = null;
      return item;
    }
  }

  public T RemoveAt(int i) {
    Node node = get(i);
    if (node.prev == null) 
      first = node.next;
    else
      node.prev.next = node.next;
    if (node.next == null) 
      last = node.prev;
    else
      node.next.prev = node.prev;       
    size--;
    stamp++;
    return node.item;
  }

  public T RemoveLast() {
    if (last == null) // and thus first == null
      throw new IndexOutOfRangeException();
    else {
      size--;
      stamp++;
      T item = last.item;
      last = last.prev;
      if (last == null) 
        first = null;
      else 
        last.next = null;
      return item;
    }
  }

  public T Remove(T item) {
    Node node = first;
    while (node != null) {
      if (item.Equals(node.item)) {
        if (node.prev == null) 
          first = node.next;
        else
          node.prev.next = node.next;
        if (node.next == null) 
          last = node.prev;
        else
          node.next.prev = node.prev;   
        size--;
	stamp++;
        return node.item;
      }
      node = node.next;
    }
    throw new ElementNotFoundException();
  }

  public bool Contains(T item) {
    Node node = first;
    while (node != null) {
      if (item.Equals(node.item)) 
        return true;
      node = node.next;
    }
    return false;
  }

  public override int GetHashCode() {
    int sum = 0;
    Node node = first;
    while (node != null) {
      sum = 31 * sum + node.item.GetHashCode();
      node = node.next;
    }
    return sum;
  }

  public override bool Equals(object that) {
    if (that is IList<T> && this.size == ((IList<T>)that).Count) {
      Node thisnode = this.first;
      IEnumerator<T> thatenm = ((IList<T>)that).GetEnumerator();
      while (thisnode != null) {
	if (!thatenm.MoveNext())
	  throw new Exception("Impossible: LinkedList<T>.Equals");
        // assert MoveNext() was true;	// because of the above size test
        if (!thisnode.item.Equals(thatenm.Current))
          return false;
        thisnode = thisnode.next; 
      }
      // assert !MoveNext(); // because of the size test
      return true;
    } else
      return false;
  }

  public IEnumerator<T> GetEnumerator() {
    return new LinkedListEnumerator(this);
  }

  class LinkedListEnumerator : IEnumerator<T> {
    LinkedList<T> lst;
    Node curr;
    int stamp;
    bool valid;
    T item;

    public LinkedListEnumerator(LinkedList<T> lst) {
      this.lst = lst; this.stamp = lst.stamp; Reset();
    }
    
    public T Current {
      get { 
	if (valid) 
	  return item; 
	else
	  throw new InvalidOperationException();
      }
    }
    
    public bool MoveNext() {
      if (stamp != lst.stamp)
	throw new InvalidOperationException(); // List modified
      else if (curr != null)  {
        item = curr.item;
        curr = curr.next;
        return valid = true;
      } else 
        return valid = false; 
    }

    public void Reset() {
      curr = lst.first; 
      valid = false;
    }
  }
}

// Array lists --------------------------------------------------
// Add(T) at end, Remove() from end; behaves like a stack, LIFO

public class ArrayList<T> : IList<T> {
  int size;			// Number of elements in list
  int stamp;			// To detect modification during enumeration
  T[] elems;			

  public ArrayList() {
    size = stamp = 0;
    elems = new T[10];  // Initial capacity
  }

  private void reallocate(int newsize) {
    T[] newelems = new T[newsize];
    for (int i=0; i<size; i++)
      newelems[i] = elems[i];
    elems = newelems;
  }

  public int Count {
    get { return size; }
  }

  public T this[int index] {
    get { return elems[index]; }
    set { elems[index] = value; }
  }      

  public bool Add(T item) {                     
    return AddLast(item);
  }

  public bool AddLast(T item) { // Add at end
    return Add(size, item);
  }

  public bool Add(int i, T item) {      // Add at position i
    if (i<0 || i>size)
      throw new IndexOutOfRangeException();
    else {
      if (size == elems.Length) 
        reallocate(2 * size);
      // assert elems.Length > size;
      for (int j=size; j>i; j--)
        elems[j] = elems[j-1];
      elems[i] = item;
      size++;
      stamp++;
      return true;
    }
  }

  public T Remove() {           // Remove last
    return RemoveAt(size-1);
  }

  public T RemoveAt(int i) {      // Remove at index i
    if (i<0 || i>=size) 
      throw new IndexOutOfRangeException();
    else {
      T item = elems[i];
      for (int j=i+1; j<size; j++)
        elems[j-1] = elems[j];
      elems[--size] = default(T); // To prevent space leaks
      stamp++;
      return item;
    }
  }

  public T Remove(T item) {     // Search 
    for (int i=0; i<size; i++)
      if (item.Equals(elems[i]))
        return RemoveAt(i);
    throw new ElementNotFoundException();
  }

  public bool Contains(T item) {
    for (int i=0; i<size; i++)
      if (item.Equals(elems[i]))
        return true;
    return false;
  }

  public override int GetHashCode() {
    int sum = 0;
    for (int i=0; i<size; i++)
      sum = 31 * sum + elems[i].GetHashCode();
    return sum;
  }

  public override bool Equals(object that) {
    if (that is IList<T> && this.size == ((IList<T>)that).Count) {
      IEnumerator<T> thatenm = ((IList<T>)that).GetEnumerator();
      for (int i=0; i<size; i++) {
	if (!thatenm.MoveNext())
	  throw new Exception("Impossible: LinkedList<T>.Equals");
        // assert MoveNext() returned true;  /// because of the size test
        if (!elems[i].Equals(thatenm.Current))
          return false;
      }
      // assert !MoveNext();  /// because of the size test
      return true;
    } else
      return false;
  }

  public IEnumerator<T> GetEnumerator() {
    return new ArrayListEnumerator(this);
  }

  class ArrayListEnumerator : IEnumerator<T> {
    ArrayList<T> lst;
    bool valid;
    int stamp;
    T item;
    int curr;

    public ArrayListEnumerator(ArrayList<T> lst) {
      this.lst = lst; stamp = lst.stamp; Reset();
    }
    
    public T Current {
      get { 
	if (valid) 
	  return item; 
	else
	  throw new InvalidOperationException();
      }
    }
    
    public bool MoveNext() {
      if (stamp != lst.stamp)  
	throw new InvalidOperationException();
      else if (curr < lst.size)  {
        item = lst[curr];
        curr++;
        return valid = true;
      } else
        return valid = false; 
    }

    public void Reset() {
      curr = 0;
      valid = false;
    }
  }
}

// ORDERED BINARY TREES (RED-BLACK TREES) -----------------------

// The root node is black
// Leaf nodes (null pointers) are black
// A red node must have a black parent
// All paths from a node to a leaf must have the same number of black nodes

// Much based on Ken Larsen's implementation for Moscow ML, whose
// deletion algorithm is inspired by Stefan Kahrs

// Nodes in binary trees 

internal class Node<K,V> {
  public Node<K,V> left, rght;
  public K key;
  public V val;
  public bool red;            // Color is red or black
  
  public Node(K key, V val) {
    this.key = key; this.val = val; red = true;
  }
  
  public Node(K key, V val, Node<K,V> left, Node<K,V> rght) {
    this.key = key; this.val = val; red = false;
  }

  // Colored rebalancing operations

  public static void lbal(ref Node<K,V> t) {
    // assert t != null;
    t.red = false;
    Node<K,V> d = t.left;
    if (d != null && d.red) {
      if (d.left != null && d.left.red) {
        d.left.red = false;
        t.left = d.rght;
        d.rght = t;
        t = d;
      } else if (d.rght != null && d.rght.red) {
        Node<K,V> bc = d.rght;
        d.red = false;
        t.left = bc.rght;
        bc.rght = t;
        d.rght = bc.left;
        bc.left = d;
        t = bc;
      }
    }
  }

  public static void rbal(ref Node<K,V> t) {
    // assert t != null;
    t.red = false;
    Node<K,V> e = t.rght;
    if (e != null && e.red) {
      if (e.rght != null && e.rght.red) {
        e.rght.red = false;
        t.rght = e.left;
        e.left = t;
        t = e;
      } else if (e.left != null && e.left.red) {
        Node<K,V> bc = e.left;
        e.red = false;
        t.rght = bc.left;
        bc.left = t;
        e.left = bc.rght;
        bc.rght = e;
        t = bc;
      }
    }
  }

  public static void balleft(ref Node<K,V> t) {
    // assert t != null;
    if (t.left != null && t.left.red) {         // (red, ---)
      t.red = true;
      t.left.red = false;
    } else if (t.rght != null)
      if (!t.rght.red) {        // (black, black)
        t.rght.red = true;
        rbal(ref t);
      } else if (t.rght.left != null && !t.rght.left.red) {
        t.red = false;
        Node<K,V> trl = t.rght.left;
        t.rght.left = trl.rght;
        if (t.rght.rght != null)
	  t.rght.rght.red = true;
        rbal(ref t.rght);
        trl.rght = t.rght;
        t.rght = trl.left;
        trl.left = t;
        t = trl;
        t.red = true;
      } else throw new Exception("balleft");
  }

  public static void balrght(ref Node<K,V> t) {
    // assert t != null;
    if (t.rght != null && t.rght.red) {         // (---, red)
      t.red = true;
      t.rght.red = false;
    } else if (t.left != null) 
      if (!t.left.red) {        // (black, black)
        t.left.red = true;
        lbal(ref t);
      } else if (t.left.rght != null && !t.left.rght.red) {
        t.red = false;
        Node<K,V> tlr = t.left.rght;
        t.left.rght = tlr.left;
        if (t.left.left != null) 
	  t.left.left.red = true;
        lbal(ref t.left);
        tlr.left = t.left;
        t.left = tlr.rght;
        tlr.rght = t;
        t = tlr;
        t.red = true;
      } else throw new Exception("balrght");
  }

  public static Node<K,V> append(Node<K,V> left, Node<K,V> rght) {
    if (left == null) 
      return rght;
    else if (rght == null) 
      return left;
    else if (left.red != rght.red) { // different colours
      if (left.red) {           // (red, black)
        left.rght = append(left.rght, rght);
        return left;
      } else {                  // (black, red)
        rght.left = append(left, rght.left);
        return rght;
      }
    } else {                    // same colours
      Node<K,V> bc = append(left.rght, rght.left);
      if (bc != null && bc.red) {
        left.rght = bc.left; 
        bc.left = left;
        rght.left = bc.rght;
        bc.rght = rght;
        return bc;
      } else {
        rght.left = bc;
        left.rght = rght;
        if (!left.red)          // (black, black)
          balleft(ref left);
        return left;
      }
    }
  }
}

// Operations on tree maps

internal interface ITreeOps<K,V> {
  bool contains(Node<K,V> t, K key);
  Node<K,V> get(Node<K,V> t, K key);
  bool add(ref Node<K,V> t, K key, V val);
  Node<K,V> del(ref Node<K,V> t, K key);
}

// Object-based IComparable tree operations ---------------------

// Object-based dynamically typed comparisons using CompareTo(object)

internal class OTreeOps<K, V> : ITreeOps<K,V> 
  where  K : System.IComparable {
  public bool contains(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return true;
    }
    return false;
  }
  
  public Node<K,V> get(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return t;
    }
    throw new ElementNotFoundException();
  }

  public bool add(ref Node<K,V> t, K key, V val) { 
    if (t == null) {
      t = new Node<K,V>(key, val);
      return true;
    } else {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0) {
        bool added = add(ref t.left, key, val);
        if (!t.red)
          Node<K,V>.lbal(ref t); 
        return added;
      } else if (cmp > 0) {
        bool added = add(ref t.rght, key, val);
        if (!t.red) 
          Node<K,V>.rbal(ref t);
        return added;
      } else 
        return false;
    }
  }

  public Node<K,V> del(ref Node<K,V> t, K key) { 
    if (t == null) {
      throw new ElementNotFoundException("TreeMap.Remove: " + key);
    } else {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0) {
        bool tleftblack = !t.left.red;
        Node<K,V> removed = del(ref t.left, key);
        if (tleftblack)  
          Node<K,V>.balleft(ref t);
        else
          t.red = true;
        return removed;
      } else if (cmp > 0) {
        bool trghtblack = !t.rght.red;
        Node<K,V> removed = del(ref t.rght, key);
        if (trghtblack)  
          Node<K,V>.balrght(ref t);
        else
          t.red = true;
        return removed;
      } else {
        Node<K,V> removed = t;
        t = Node<K,V>.append(t.left, t.rght);
        return removed;
      }    
    }
  }
}

// Generic IComparable<K> tree operations ---------------------

// Generic statically typed implicit comparer CompareTo(K).

// The code is textually identical to the above, but will avoid
// all the boxings implied by the above code when K is
// instantiated to a value type.

internal class GTreeOps<K, V> : ITreeOps<K,V> 
  where K : IComparable<K> {
  public bool contains(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return true;
    }
    return false;
  }
  
  public Node<K,V> get(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return t;
    }
    throw new ElementNotFoundException();
  }

  public bool add(ref Node<K,V> t, K key, V val) { 
    if (t == null) {
      t = new Node<K,V>(key, val);
      return true;
    } else {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0) {
        bool added = add(ref t.left, key, val);
        if (!t.red)
          Node<K,V>.lbal(ref t); 
        return added;
      } else if (cmp > 0) {
        bool added = add(ref t.rght, key, val);
        if (!t.red) 
          Node<K,V>.rbal(ref t);
        return added;
      } else 
        return false;
    }
  }

  public Node<K,V> del(ref Node<K,V> t, K key) { 
    if (t == null) {
      throw new ElementNotFoundException("TreeMap.Remove: " + key);
    } else {
      int cmp = key.CompareTo(t.key);
      if (cmp < 0) {
        bool tleftblack = !t.left.red;
        Node<K,V> removed = del(ref t.left, key);
        if (tleftblack)  
          Node<K,V>.balleft(ref t);
        else
          t.red = true;
        return removed;
      } else if (cmp > 0) {
        bool trghtblack = !t.rght.red;
        Node<K,V> removed = del(ref t.rght, key);
        if (trghtblack)  
          Node<K,V>.balrght(ref t);
        else
          t.red = true;
        return removed;
      } else {
        Node<K,V> removed = t;
        t = Node<K,V>.append(t.left, t.rght);
        return removed;
      }    
    }
  }
}

// Separate statically typed comparer: Compare(K,K)

internal class FGTreeOps<K,V> : ITreeOps<K,V> {
  IComparer<K> comparer;

  public FGTreeOps(IComparer<K> comparer) { 
    this.comparer = comparer;
  }

  public bool contains(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return true;
    }
    return false;
  }
  
  public Node<K,V> get(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return t;
    }
    throw new ElementNotFoundException();
  }

  public bool add(ref Node<K,V> t, K key, V val) { 
    if (t == null) {
      t = new Node<K,V>(key, val);
      return true;
    } else {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0) {
        bool added = add(ref t.left, key, val);
        if (!t.red)
          Node<K,V>.lbal(ref t); 
        return added;
      } else if (cmp > 0) {
        bool added = add(ref t.rght, key, val);
        if (!t.red) 
          Node<K,V>.rbal(ref t);
        return added;
      } else 
        return false;
    }
  }

  public Node<K,V> del(ref Node<K,V> t, K key) { 
    if (t == null) {
      throw new ElementNotFoundException("TreeMap.Remove: " + key);
    } else {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0) {
        bool tleftblack = !t.left.red;
        Node<K,V> removed = del(ref t.left, key);
        if (tleftblack)  
          Node<K,V>.balleft(ref t);
        else
          t.red = true;
        return removed;
      } else if (cmp > 0) {
        bool trghtblack = !t.rght.red;
        Node<K,V> removed = del(ref t.rght, key);
        if (trghtblack)  
          Node<K,V>.balrght(ref t);
        else
          t.red = true;
        return removed;
      } else {
        Node<K,V> removed = t;
        t = Node<K,V>.append(t.left, t.rght);
        return removed;
      }    
    }
  }
}

// Separate dynamically typed comparer: Compare(object, object) 

internal class FOTreeOps<K,V> : ITreeOps<K,V> {
  System.Collections.IComparer comparer;

  public FOTreeOps(System.Collections.IComparer comparer) { 
    this.comparer = comparer;
  }

  public bool contains(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return true;
    }
    return false;
  }
  
  public Node<K,V> get(Node<K,V> t, K key) {
    while (t != null) {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0)
        t = t.left;
      else if (cmp > 0) 
        t = t.rght;
      else 
        return t;
    }
    throw new ElementNotFoundException();
  }

  public bool add(ref Node<K,V> t, K key, V val) { 
    if (t == null) {
      t = new Node<K,V>(key, val);
      return true;
    } else {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0) {
        bool added = add(ref t.left, key, val);
        if (!t.red)
          Node<K,V>.lbal(ref t); 
        return added;
      } else if (cmp > 0) {
        bool added = add(ref t.rght, key, val);
        if (!t.red) 
          Node<K,V>.rbal(ref t);
        return added;
      } else 
        return false;
    }
  }

  public Node<K,V> del(ref Node<K,V> t, K key) { 
    if (t == null) {
      throw new ElementNotFoundException("TreeMap.Remove: " + key);
    } else {
      int cmp = comparer.Compare(key, t.key);
      if (cmp < 0) {
        bool tleftblack = !t.left.red;
        Node<K,V> removed = del(ref t.left, key);
        if (tleftblack)  
          Node<K,V>.balleft(ref t);
        else
          t.red = true;
        return removed;
      } else if (cmp > 0) {
        bool trghtblack = !t.rght.red;
        Node<K,V> removed = del(ref t.rght, key);
        if (trghtblack)  
          Node<K,V>.balrght(ref t);
        else
          t.red = true;
        return removed;
      } else {
        Node<K,V> removed = t;
        t = Node<K,V>.append(t.left, t.rght);
        return removed;
      }    
    }
  }
}

// The TreeMap class itself, and two subclasses

public class TreeMap<K, V> : ISortedMap<K,V> {
  private int size;		// Number of entries in the tree map
  private int stamp;		// To detect modification during enumeration
  private Node<K,V> root;
  private ITreeOps<K,V> treeops;

  internal TreeMap(ITreeOps<K,V> treeops) {
    size = stamp = 0; 
    root = null; 
    this.treeops = treeops;
  }

  // Object-based dynamically typed explicit comparer

  public TreeMap(System.Collections.IComparer comparer) 
    : this(new FOTreeOps<K,V>(comparer)) { }

  // Generic statically typed explicit comparer

  public TreeMap(IComparer<K> comparer) 
    : this(new FGTreeOps<K,V>(comparer)) { }

  public int Count {
    get { return size; }
  }
  
  public bool Add(K key, V val) {
    bool added = treeops.add(ref root, key, val);
    root.red = false;
    if (added) {
      size++;
      stamp++;
    }
    return added;
  }

  public V this[K key] { 
    get { return treeops.get(root, key).val; }
    set { 
      if (treeops.contains(root, key)) 
	treeops.get(root, key).val = value; 
      else
	treeops.add(ref root, key, value);
    }
  }

  public bool Contains(K key) {
    return treeops.contains(root, key);
  }

  // Remove item from set and return it; or throw ElementNotFoundException

  public MapEntry<K,V> Remove(K key) { 
    Node<K,V> res = treeops.del(ref root, key);
    if (root != null)
      root.red = false;
    size--;
    stamp++;
    return new MapEntry<K,V>(res.key, res.val);
  }

  // Computing the tree depth (for debugging only)

  public int Depth() {
    return depth(root);
  }

  private static int depth(Node<K,V> node) {
    if (node == null) 
      return 0;
    else 
      return 1 + System.Math.Max(depth(node.left), depth(node.rght));
  }

  // Issue: these functions could be optimized to not use the enumerators

  public override int GetHashCode() {
    int sum = 0;
    foreach (MapEntry<K,V> entry in this)
      sum += entry.Key.GetHashCode() ^ entry.Value.GetHashCode();
    return sum;
  }

  public override bool Equals(object that) {
    if (that is IMap<K,V> && Count == ((IMap<K,V>)that).Count) {
      IMap<K,V> thatMap = (IMap<K,V>)that;
      if (thatMap is ISortedMap<K,V>) 
	return equalsSortedMap((ISortedMap<K,V>)thatMap);
      else {
	IEnumerator< MapEntry<K,V> > thisenm = this.GetEnumerator();
	while (thisenm.MoveNext()) {
	  MapEntry<K,V> entry = thisenm.Current;
	  if (!thatMap.Contains(entry.Key) 
	      || !thatMap[entry.Key].Equals(entry.Value))
	    return false;
	}
	return true;
      }
    } else
      return false;
  }

  // More efficient comparison possible when both maps are sorted

  private bool equalsSortedMap(ISortedMap<K,V> thatMap) {
    // assert this.Count == thatMap.Count;
    IEnumerator< MapEntry<K,V> > thisenm = this.GetEnumerator();
    IEnumerator< MapEntry<K,V> > thatenm = thatMap.GetEnumerator();
    while (thisenm.MoveNext() && thatenm.MoveNext()) {
      MapEntry<K,V> tit = thisenm.Current;
      MapEntry<K,V> tat = thatenm.Current;
      if (!tit.Key.Equals(tat.Key) || !tit.Value.Equals(tat.Value))
	return false;
    }
    // assert both thisenm and thatenm are at end, because of the size test
    return true;
  }

  // Do an inorder forwards traversal of the tree

  public IEnumerator<MapEntry<K,V>> GetEnumerator() {
    return new TreeEnumerator(this);
  }

  private class TreeEnumerator : IEnumerator<MapEntry<K,V>> {
    ArrayList< Node<K,V> > stack;
    TreeMap<K,V> tree;
    bool valid;
    int stamp;
    K key;
    V val;

    public TreeEnumerator(TreeMap<K,V> tree) {
      this.tree = tree; stamp = tree.stamp;
      Reset();
    }
    
    // Issue: should probably not create a new struct on every invocation
    public MapEntry<K,V> Current {
      get { 
	if (valid)
	  return new MapEntry<K,V>(key, val); 
	else
	  throw new InvalidOperationException();
      }
    }
    
    public bool MoveNext() {
      if (stamp != tree.stamp) 
	throw new InvalidOperationException();
      else if (stack.Count > 0) {
        Node<K,V> node = stack.Remove(); 
        // assert node != null;
        while (node.left != null) {
          push(node.rght);				// Push right branch
          stack.Add(new Node<K,V>(node.key, node.val)); // Push node item
          node = node.left;				// Descend left branch
        }
        push(node.rght);
        key = node.key;
        val = node.val;
        return valid = true;
      } else
        return valid = false; 
    }

    private void push(Node<K,V> node) {
      if (node != null)
        stack.Add(node);
    }

    public void Reset() {
      stack = new ArrayList< Node<K,V> >();
      push(tree.root);
      valid = false;
    }
  }
}

// Object-based implicit CompareTo 

public class OTreeMap<K, V> : TreeMap<K,V> 
  where K : System.IComparable {
  public OTreeMap() : base(new OTreeOps<K,V>()) { }
}

// Generic statically typed implicit CompareTo 

public class GTreeMap<K, V> : TreeMap<K,V> 
  where K : IComparable<K> {
  public GTreeMap() : base(new GTreeOps<K,V>()) { }
}

// We cannot do the latter two versions as constructors in TreeMap
// because they need (different) constraints on type parameter K.


// TreeSet (in terms of TreeMap) -----------------------------

public class TreeSet<T> : ISortedSet<T> {
  private TreeMap<T, int> map;	// The int in the treemap is unused

  internal TreeSet(TreeMap<T, int> map) {
    this.map = map;
  }

  // Object-based dynamically typed explicit comparer

  public TreeSet(System.Collections.IComparer comparer) 
    : this(new TreeMap<T,int>(comparer)) { }

  // Generic statically typed explicit comparer

  public TreeSet(IComparer<T> comparer) 
    : this(new TreeMap<T,int>(comparer)) { }

  public int Count {
    get { return map.Count; }
  }

  public bool Add(T item) {
    return map.Add(item, 0);
  }
  
  public T Remove(T item) { 
    return map.Remove(item).Key;  
  }

  public bool Contains(T item) { 
    return map.Contains(item);  
  }

  public IEnumerator<T> GetEnumerator() {
    return new TreeEnumerator(map.GetEnumerator());
  }

  private class TreeEnumerator : IEnumerator<T> {
    private IEnumerator<MapEntry<T,int>> enm;
    
    public TreeEnumerator(IEnumerator< MapEntry<T,int> > enm) {
      this.enm = enm;
    }

    public T Current {
      get { return enm.Current.Key; }
    }
    
    public bool MoveNext() {
      return enm.MoveNext();
    }

    public void Reset() {
      enm.Reset();
    }
  }

  public override int GetHashCode() {
    int sum = 0;
    foreach (T item in this)
      sum += item.GetHashCode();
    return sum;
  }

  public override bool Equals(object that) {
    if (that is ISet<T> && Count == ((ISet<T>)that).Count) {
      ISet<T> thatSet = (ISet<T>)that;
      if (thatSet is ISortedSet<T>) 
	return equalsSortedSet((ISortedSet<T>)thatSet);
      else {
	IEnumerator<T> thisenm = this.GetEnumerator();
	while (thisenm.MoveNext()) {
	  if (!thatSet.Contains(thisenm.Current)) 
	    return false;
	}
	return true;
      }
    } else
      return false;
  }

  // More efficient comparison possible when both sets are sorted

  private bool equalsSortedSet(ISortedSet<T> thatSet) {
    // assert this.Count == thatSet.Count;
    IEnumerator<T> thisenm = this.GetEnumerator();
    IEnumerator<T> thatenm = thatSet.GetEnumerator();
    while (thisenm.MoveNext() && thatenm.MoveNext())
      if (!thisenm.Current.Equals(thatenm.Current))
	return false;
    // assert both thisenm and thatenm are at end, because of the size test
    return true;
  }
}

// Object-based implicit CompareTo 

public class OTreeSet<T> : TreeSet<T> 
  where T : System.IComparable {
  public OTreeSet() : base(new OTreeMap<T,int>()) { }
}

// Generic statically typed implicit CompareTo 

public class GTreeSet<T> : TreeSet<T> 
  where T : IComparable<T> {
  public GTreeSet() : base(new GTreeMap<T,int>()) { }
}

// We cannot do the latter two versions as constructors in TreeSet
// because they need (different) constraints on type parameter T.


// Exceptions ------------------------------------------------

class ElementNotFoundException : Exception { 
  public ElementNotFoundException() : base() { }
  public ElementNotFoundException(string s) : base(s) { }
} 
} // End of namespace GCollections
