//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
#if CCINamespace
namespace Microsoft.Cci{
#else
namespace System.Compiler{
#endif
  using System;
  using System.Text;
  using System.Collections;
  using System.IO;
  using System.Collections.Specialized;
  using System.Diagnostics;

  public interface IIndexable : IEnumerable {
    object this[int index] { get; }
		
    int Length { get; }
  }


  internal class GenericEnumerator : IEnumerator {
    public delegate object Indexer(int index);

    private int index;
    private Indexer indexer;
    private int size;

    public GenericEnumerator (Indexer indexer, int size) {
      this.indexer = indexer;
      this.index = -1;
      this.size = size;
    }

    #region IEnumerator Members

    public void Reset() {
      this.index = -1;
    }

    public object Current {
      get {
        return indexer(index);
      }
    }

    public bool MoveNext() {
      index++;
      return (index < size);
    }

    #endregion
  }


  public class ArrayEnumerable : IEnumerable
  {
    object[] array;
    int size;

    public ArrayEnumerable(object[] array, int size) 
    {
      this.array = array;
      this.size = size;
    }

    #region IEnumerable Members

    public IEnumerator GetEnumerator()
    {
      return new ArrayEnumerator(this.array, this.size);
    }

    #endregion

  }



  /// <summary>
  /// Enumerator over a prefix of an array (System.Array.GetEnumerator returns
  /// an enumerator over ALL the elements of the array).
  /// </summary>
  public class ArrayEnumerator : IEnumerator
  {
    /// <summary>
    /// Constructs an enumerator  over the first <c>size</c> elements of <c>array</c>.
    /// NOTE: I couldn't find any way of detecting comodification errors ...
    /// </summary>
    public ArrayEnumerator(object[] array, int size)
    {
      this.array = array;
      this.size  = size;
      this.index = -1;
    }

    // underlying array
    private readonly object[] array;
    // length of the prefix of array that we enumerate over
    private readonly int      size;
    // current enumerator position
    private int index;


    public virtual object Current
    {
      get 
      {
        if((index >= 0) && (index < size)) return array[index];
        else
          throw new InvalidOperationException();
      }
    }


    public virtual bool MoveNext()
    {
      if(index == size)
        return false;
      index++;
      // check whether we've passed the limit or not
      return index != size;
    }


    public virtual void Reset()
    {
      index = -1;
    }
  }




  public delegate IMutableSet DSetFactory();
  public delegate IDictionary DDictionaryFactory();

  /// <summary>
  /// Summary description for DataStructsUtil.
  /// </summary>
  public abstract class DataStructUtil
  {	
    public static readonly DSetFactory DefaultSetFactory = new DSetFactory(default_set_factory);
    private static IMutableSet default_set_factory()
    {
      return new HashSet(1);
    }


    public static readonly DSetFactory SmallSetFactory = new DSetFactory(small_set_factory);
    private static IMutableSet small_set_factory()
    {
      return new ArraySet(1);
    }


    public static readonly DSetFactory NodeSetFactory = new DSetFactory(node_set_factory);

    private static IMutableSet node_set_factory()
    {
      return new NodeSet();
    }

    public static readonly DDictionaryFactory DefaultDictionaryFactory = new DDictionaryFactory(default_dict_factory);
    private static IDictionary default_dict_factory()
    {
      return new Hashtable(1);
    }
		

    public static readonly DDictionaryFactory SmallDictionaryFactory = new DDictionaryFactory(small_dict_factory);
    private static IDictionary small_dict_factory()
    {
      return new ListDictionary();
    }
		

    public static ISet EMPTY_SET = new ImmutableSetWrapper(new HashSet());

    public static ISet singleton(object elem)
    {
      return new SingletonSet(elem);
    }


    public static IList SortedCollection(ICollection coll)
    {
      return SortedCollection(coll, null);
    }

    public static IList SortedCollection(ICollection coll, IComparer comp)
    {
      ArrayList al = new ArrayList(coll);
      if(comp == null)
        al.Sort();
      else
        al.Sort(comp);
      return al;
    }

    /// <summary>
    /// Produces a string representation of an <c>IEnumerable</c> object,
    /// using <c>o2s</c> to produce the string representation of each
    /// element.
    /// </summary>
    /// <param name="eable"></param>
    /// <param name="o2s"></param>
    /// <returns></returns>
    /// 
    public static string IEnum2String(IEnumerable eable, DObj2String o2s)
    {
      StringBuilder buff = new StringBuilder();
      buff.Append("[");
      bool first = true;
      foreach(object o in eable)
      {
        if(first) first = false;
        else buff.Append(",");
        if(o2s != null)
          buff.Append(o2s(o));
        else
          buff.Append(o);
      }
      buff.Append("]");
      return buff.ToString();
    }


    public readonly static IComparer NodeComparer = NodeCompareClass.Comparer;

    private class NodeCompareClass : IComparer
    {
      private NodeCompareClass() {}

      public static readonly IComparer Comparer = new NodeCompareClass();

      #region IComparer Members

      public int Compare(object x, object y)
      {
        Node n1 = (Node)x;
        Node n2 = (Node)y;

        return n1.UniqueKey - n2.UniqueKey;
      }

      #endregion

    }

    public readonly static IComparer NodeIdComparer = NodeIdCompareClass.Comparer;

    private class NodeIdCompareClass : IComparer {
      private NodeIdCompareClass() {}

      public static readonly IComparer Comparer = new NodeIdCompareClass();

      #region IComparer Members

      public int Compare(object x, object y) {
        IUniqueKey n1 = (IUniqueKey)x;
        IUniqueKey n2 = (IUniqueKey)y;

        return n2.UniqueId - n1.UniqueId;
      }

      #endregion

    }

    public readonly static IComparer CfgBlockComparer = CfgBlockCompareClass.Comparer;

    private class CfgBlockCompareClass : IComparer {
      private CfgBlockCompareClass() {}

      public static readonly IComparer Comparer = new CfgBlockCompareClass();

      #region IComparer Members

      public int Compare(object x, object y) {
        CfgBlock b1 = (CfgBlock)x;
        CfgBlock b2 = (CfgBlock)y;

        return b1.Index - b2.Index;
      }

      #endregion

    }


  }

  /// <summary>
  /// Conversion object -> string. Useful for classes for which we cannot
  /// modify / override the <c>ToString</c> method. A <c>null</c> value
  /// should be interpreted as the classic <c>ToString</c> method.
  /// </summary>
  public delegate string DObj2String(object obj);

  internal class SingletonSet: ISet
  {
    private object elem;

    public SingletonSet(object elem)
    {
      this.elem = elem;
    }

    public bool Contains(object elem)
    {
      return this.elem.Equals(elem);
    }

    public int Count { get { return 1; } }

    public override int GetHashCode()
    {
      return elem.GetHashCode();
    }

    public IEnumerator GetEnumerator()
    {
      return new SingletonEnumerator(elem);
    }

    private class SingletonEnumerator: IEnumerator
    {
      private object elem;
      private int index = -1;
      public SingletonEnumerator(object elem)
      {
        this.elem = elem;
        Reset();
      }

      public virtual object Current
      {
        get 
        {
          if(index == 0) return elem;
          else
            throw new InvalidOperationException();
        }
      }

      public virtual bool MoveNext()
      {
        switch(index)
        {
          case -1:
            index++;
            return true;
          case 0:
            index++;
            return false;
          case 1:
          default:
            return false;
        }
      }

      public virtual void Reset()
      {
        index = -1;
      }
    }

    public object Copy()
    {
      SingletonSet copy = (SingletonSet) this.MemberwiseClone();
      copy.elem = (this.elem is ICopyable) ? ((ICopyable) this.elem).Copy() : this.elem;
      return copy;
    }

    void ICollection.CopyTo (Array target, int index) 
    {
      target.SetValue(this.elem, index);
    }

    object ICollection.SyncRoot 
    {
      get 
      {
        return this;
      }
    }

    bool ICollection.IsSynchronized 
    {
      get { return false; }
    }
  }
	
	
	
  public class OneToManyMap/*<KEY,ELEM>*/ : Hashtable/*<KEY,ArrayList<ELEM>>*/
  {
    public override void Add (Object key, Object newItem)
    {
      ArrayList items = base[key] as ArrayList;
      if (items == null)
      {
        items = new ArrayList();
        base[key] = items;
      }
      items.Add(newItem);
    }


    public virtual bool Contains (Object key, Object item)
    {
      ArrayList items = base[key] as ArrayList;
      if (items == null)
        return false;
      return items.Contains(item);
    }


    public new ArrayList/*<Object>*/ this [Object key] 
    {
      get { return base[key] as ArrayList; }
      set { base[key] = value; }
    }

  }

  public class DoubleFunctionalMap/*<A:IUniqueKey, B:IUniqueKey, C>*/ {

    private IFunctionalMap/*<A, IFunctinalMap<B,C>*/ map;

    public object this[IUniqueKey key1, IUniqueKey key2] {
      get {
        IFunctionalMap t = (IFunctionalMap)this.map[key1];

        if (t == null) {
          return null;
        }

        return t[key2];
      }
    }

    public DoubleFunctionalMap Add(IUniqueKey key1, IUniqueKey key2, object value) {
      IFunctionalMap t = (IFunctionalMap)this.map[key1];

      if (t == null) {
        t = FunctionalMap.Empty;
      }

      return new DoubleFunctionalMap(this.map.Add(key1, t.Add(key2, value)));
    }


    public DoubleFunctionalMap RemoveAll(IUniqueKey key1) {
      return new DoubleFunctionalMap(this.map.Remove(key1));
    }

    public DoubleFunctionalMap Remove(IUniqueKey key1, IUniqueKey key2) {
      IFunctionalMap t = (IFunctionalMap)this.map[key1];

      if (t == null) { return this; }

      return new DoubleFunctionalMap(this.map.Add(key1, t.Remove(key2)));
    }

    private DoubleFunctionalMap(IFunctionalMap map) {
      this.map = map;
    }

    public static DoubleFunctionalMap Empty = new DoubleFunctionalMap(FunctionalMap.Empty);

    public bool ContainsKey1(/*<A>*/ IUniqueKey key1) {
      return this.map[key1] != null;
    }

    public ICollection/*<A>*/ Keys1 {
      get { return this.map.Keys; }
    }

    public int Keys2Count(/*A*/IUniqueKey key1) { 
      IFunctionalMap map2 = (IFunctionalMap)this.map[key1];
        
      if (map2 == null) { return 0; }
      return map2.Count;
    }

    public ICollection/*<B>*/ Keys2(/*A*/IUniqueKey key1) {
      IFunctionalMap map2 = (IFunctionalMap)this.map[key1];
        
      if (map2 == null) { return new object[0]; }
      return map2.Keys;
    }

  }



  public class DoubleTable/*<KEY1,KEY2,ELEM>*/ : Hashtable/*<KEY1,Hashtable<KEY2,ELEM>>*/
  {
    public DoubleTable () : base() { }

    private DoubleTable (Hashtable table) : base(table) { }


    public new Hashtable this [object key]
    {
      get 
      {
        return base[key] as Hashtable;
        /*
          if (table == null)
          {
            table = new Hashtable();
            base[key] = table;
          }
          return table;
          */
      }

      set
      {
        base[key] = value;
      }
    }


    public void Insert (object key1, object key2, object element)
    {
      Hashtable table = this[key1];
      if (table == null)
      {
        table = new Hashtable();
        this[key1] = table;
      }
      table[key2] = element;
    }


    public object Lookup (object key1, object key2) 
    {
      Hashtable range = this[key1];
      if (range != null) 
      {
        return range[key2];
      }
      else
      {
        return null;
      }
    }
		
    public override object Clone ()
    {
      // careful. We have to deep clone the targets of the underlying hashtable.
      DoubleTable copy = new DoubleTable();
      foreach (object key in this.Keys) 
      {
        Hashtable x = this[key];

        Debug.Assert(x != null);
        copy.Add(key, x.Clone());
      }
      return copy;
    }

    public DoubleTable Copy() 
    {
      return (DoubleTable)this.Clone();
    }
  }


	
	
  /// <summary>
  /// An attempt to make coding with datatypes more pleasant in C#
  /// </summary>
  /// <remarks>
  /// Use <c>Datatype</c> as a base class on each of your abstract data type base classes. When doing
  /// case analysis on such a value, use the method <c>Tag</c> to get a String representation of the 
  /// dynamic class name. This can be matched in the <c>case</c> branches of a switch. 
  /// <p>
  /// For pairwise matching, use <c>x.Tag + y.Tag</c> and <c>case "Foo"+"Bar"</c>.
  /// </p>
  /// Can be extended to compute the set of tags of each abstract datatype instance and utility methods.
  /// </remarks>
  public abstract class Datatype 
  {
    private int tag;

    private static Hashtable/*<Type,DatatypeInfo>*/ typeTable = new Hashtable();

    private class DatatypeInfo 
    {
      IList/*<Type>*/ members;
      Type abstractBaseType;
      public DatatypeInfo(Type abstractBaseType) 
      {
        members = new ArrayList();
        this.abstractBaseType = abstractBaseType;
      }

      public void Add(Type variant) 
      {
        members.Add(variant);
      }

      public ICollection Members { get { return members; } }
    }

    private void AddToTable (Type variant) 
    {
      DatatypeInfo info = GetBaseInfo(variant);
      info.Add(variant);
      typeTable[variant] = info;
    }

    private DatatypeInfo GetBaseInfo (Type variant) 
    {
      Type abase = FindBase(variant);
      DatatypeInfo info = (DatatypeInfo)Datatype.typeTable[abase];
      if (info == null) 
      {
        // first time we see anything in this class tree.
        info = new DatatypeInfo(abase);
        Datatype.typeTable[abase] = info;
      }
      return info;
    }

    private Type FindBase(Type variant) 
    {
      Type abase = variant.BaseType;
      if (abase.IsAbstract) 
      {

        // check that it is not datatype
        if (abase == typeof(Datatype)) 
        {
          Debug.Assert(false, "Every datatype should have an abstract base class that is a subtype of Datatype: " + variant.Name + " does not.");
        }

        // check if its base is Datatype
        if (abase.BaseType == typeof(Datatype)) 
        {
          return abase;
        }
        else 
        {
          // intermediate abstract base
          return FindBase(abase);
        }
      }
      // recurse until we find abstract base
      return FindBase(abase);
    }

    protected Datatype (int tag) 
    {
      Type realType = this.GetType();

      this.tag = tag; //realType.Name;
      if (typeTable[realType] == null) 
      {
        // add this first instance to the type table
        this.AddToTable(realType);
      }
    }

    public virtual int Tag
    {
      get { return tag; }
    }

    public void IncompleteMatch() 
    {
      Type realType = this.GetType();

      DatatypeInfo info = (DatatypeInfo)Datatype.typeTable[realType];

      Debug.Assert(info != null, "No datatype info for type " + realType.Name);

      System.Text.StringBuilder buffer = new StringBuilder();
      foreach (object member in info.Members) 
      {
        Type m = member as Type;

        Debug.Assert(m != null);

        buffer.Append(" ");
        buffer.Append(m.Name);
      }

      Console.Write("Incomplete match: {0} was not matched.\nDatatype contains the following variants {1}\n",
        this.Tag, buffer.ToString());
      Debug.Assert(false, "incomplete match");
    }
  }

  public class ConvertingEnumerable : IEnumerator, IEnumerable
  {
    public delegate object ObjectConverter(object o);

    private IEnumerator underlying;
    private ObjectConverter objConverter;

    public ConvertingEnumerable(IEnumerable underlying, ObjectConverter objConverter)
    {
      this.underlying = underlying.GetEnumerator();
      this.objConverter = objConverter;
      Reset();
    }

    public object Current
    {
      get 
      {
        return objConverter(underlying.Current);
      }
    }

    public bool MoveNext()
    {
      return underlying.MoveNext();
    }

    public void Reset()
    {
      underlying.Reset();
    }

    public IEnumerator GetEnumerator()
    {
      return this;
    }
  }




  /// <summary>
  /// Functional lists. null represents the empty list.
  /// </summary>
  public class FList : IEnumerable
  {

    public delegate string Stringer(object element);

    private object elem;

    private FList tail;

    private int count;

    public int Count { get { return count; } }

    public FList(object elem, FList tail)
    {
      this.elem = elem;
      this.tail = tail;
      this.count = FList.Length(tail) + 1;
    }


    public object Head { get { return this.elem; } }
    public FList Tail 
    { 
      get { return this.tail; } 
    }


    public static FList Cons(object elem, FList rest) 
    {
      return new FList(elem, rest);
    }


    public static readonly FList Empty = null;

    public static int Length(FList l) 
    {
      if (l == null) { return 0; } 
      else return l.count;
    }


    public static FList Reverse(FList list) 
    {
      FList tail = null;

      while (list != null) 
      {
        tail = FList.Cons(list.elem, tail);
        list = list.tail;
      }
      return tail;
    }


    private class FListEnumerator : IEnumerator
    {
      readonly FList first;
      FList rest;
      bool beforefirst;

      public FListEnumerator(FList first) 
      {
        this.first = first;
        beforefirst = true;
      }

      public object Current
      {
        get 
        {
          if (rest != null) 
          {
            return rest.Head;
          }
          else throw new InvalidOperationException("enumerator at invalid point.");
        }
      }

      public bool MoveNext() 
      {
        if (beforefirst) 
        {
          this.beforefirst = false;
          this.rest = this.first;
          return (rest != null);
        }

        if (rest != null) 
        {
          rest = rest.Tail;
          return (rest != null);
        }
        return false;
      }

      public void Reset() 
      {
        this.beforefirst = true;
      }
    }


    public IEnumerator GetEnumerator() 
    {
      return new FListEnumerator(this);
    }


    public static bool Contains(FList l, object o) 
    {
      if (l==null) return false;

      if (Equals(l.elem, o)) return true;

      return Contains(l.tail, o);
    }




    /// <summary>
    /// Given two sorted lists, compute their intersection
    /// </summary>
    /// <param name="l1">sorted list</param>
    /// <param name="l2">sorted list</param>
    /// <returns>sorted intersection</returns>
    public static FList Intersect(FList l1, FList l2) 
    {
      if (l1 == null || l2 == null) return null;

      int comp = System.Collections.Comparer.Default.Compare(l1.Head, l2.Head);
      if (comp < 0) 
      {
        return Intersect(l1.Tail, l2);
      }
      if (comp > 0) 
      {
        return Intersect(l1, l2.Tail);
      }
      // equal
      return FList.Cons(l1.Head, Intersect(l1.Tail, l2.Tail));
    }

    public static FList Sort(FList l) 
    {
      return Sort(l, null);
    }

    public static FList Sort(FList l, FList tail) 
    {
      // quicksort
      if (l == null) return tail;

      object pivot = l.Head;

      FList less;
      FList more;
      Partition(l.Tail, pivot, out less, out more);
			
      return Sort(less, FList.Cons(pivot, Sort(more, tail)));
    }

    private static void Partition(FList l, object pivot, out FList less, out FList more) 
    {
      less = null;
      more = null;
      if (l == null) 
      {
        return;
      }
      foreach(object value in l) 
      {
        if (System.Collections.Comparer.Default.Compare(value, pivot) <= 0)
        {
          less = FList.Cons(value, less);
        }
        else 
        {
          more = FList.Cons(value, more);
        }
      }
    }

    public static FList Append(FList l1, FList l2) 
    {
      if (l1 == null) return l2;

      if (l2 == null) return l1;

      return FList.Cons(l1.elem, Append(l1.tail, l2));
    }

    public String ToString(Stringer elemstringer) 
    {
      StringBuilder sb = new StringBuilder();

      this.BuildString(sb, elemstringer);
      return sb.ToString();
    }

    public void BuildString(StringBuilder sb, Stringer elemstringer) 
    {
      if (this.tail != null) 
      {
        sb.AppendFormat("{0},", elemstringer(this.elem));
        this.tail.BuildString(sb, elemstringer);
      }
      else 
      {
        sb.Append(elemstringer(this.elem));
      }
    }
  }




  

  /// <summary>
  /// Represents an object that can be copied deeply, as opposed to the shallow ICloneable.
  /// </summary>
  public interface ICopyable
  {
    object Copy();
  }

  
  
  
  /// <summary>
  /// An abstraction for maps
  /// </summary>
  public interface IMap : ICopyable
  {

    object this[object key] { get; set; }

    void Add(object key, object val);
    void Remove(object key);
    bool Contains(object key);

    ICollection Keys { get; }

  }





  /// <summary>
  /// A Map based on a hashtable.
  /// </summary>
  public class HashedMap : Hashtable, IMap
  {
    public HashedMap(int capacity) : base(capacity) { }

    public HashedMap() { }


    // Copy constructor
    protected HashedMap (HashedMap old) 
      : base(old.Count)
    {
      foreach(object key in old.Keys) 
      {
        object value = old[key];
        ICopyable cvalue = value as ICopyable;
        if (cvalue != null) 
        {
          this[key] = cvalue.Copy();
        }
        else 
        {
          this[key] = value;
        }
      }
    }

    object ICopyable.Copy() 
    {
      return this.Copy();
    }

    /// <summary>
    /// Deep copy. Checks if values implement ICopyable. If so, copies them too.
    /// </summary>
    public HashedMap Copy() 
    {
      return new HashedMap(this);
    }
  }

  /// <summary>
  /// A Map based on a ListDictionary
  /// </summary>
  public class ListMap : ListDictionary, IMap
  {
    public ListMap() { }

    public ListMap(IComparer c) : base(c) { }
		
    protected ListMap(ListMap old) 
    {
      foreach(object key in old.Keys) 
      {
        object value = old[key];
        ICopyable cvalue = value as ICopyable;
        if (cvalue != null) 
        {
          this[key] = cvalue.Copy();
        }
        else 
        {
          this[key] = value;
        }
      }
    }

    object ICopyable.Copy() 
    {
      return this.Copy();
    }

    /// <summary>
    /// Deep copy. Checks if values implement ICopyable. If so, copies them too.
    /// </summary>
    public ListMap Copy() 
    {
      return new ListMap(this);
    }
  }



  public enum VisitStatus { ContinueVisit, StopVisit }

  public delegate VisitStatus VisitMapPair (object key, object value);



  public interface IFunctionalIntMap 
  {
    /*
     * Ideally, the key type in this API would be 
     * an interface IHasUniqueId with a single property
     * that return an int. However, in our use of this
     * API, we need to plug in objects that cannot be
     * unified under a common interface because we don't 
     * own some of the classes. So, awkwardly, we ask
     * the client to pass in BOTH a key object and its
     * integer under which it will be stored.
     */

    object Lookup (int keyNumber);
    object this[int keyNumber] { get; }
    IFunctionalIntMap Add (int keyNumber, object key, object val);
    ICollection/*<object>*/ Keys { get; }
    void Visit (VisitMapPair visitor);
    IFunctionalIntMap Remove (int keyNumber);
    int Count { get; }

    void Dump (System.IO.TextWriter tw);
  }

  public interface IFunctionalMap {

    object this[IUniqueKey key] { get; }
    IFunctionalMap Add (IUniqueKey key, object val);
    IFunctionalMap Remove (IUniqueKey key);

    ICollection/*<IUniqueKey>*/ Keys { get; }
    void Visit (VisitMapPair visitor);
    int Count { get; }

    void Dump (System.IO.TextWriter tw);
  }

  public interface IFunctionalSet {

    bool this[IUniqueKey key] { get; }
    IFunctionalSet Add (IUniqueKey key);
    IFunctionalSet Remove (IUniqueKey key);

    ICollection/*<IUniqueKey>*/ Elements { get; }
    int Count { get; }

    void Dump (System.IO.TextWriter tw);
  }


  public class FunctionalIntMap
  {
    // Adapted from Okasaki and Gill, "Fast mergeable integer maps", ML Workshop, 1998.

    public static IFunctionalIntMap EmptyMap { get { return Empty.E; } }




    private abstract class PatriciaTree : IFunctionalIntMap
    {
      public abstract object Lookup (int keyNumber);

      public object this[int keyNumber] { get { return this.Lookup(keyNumber); } }

      public abstract IFunctionalIntMap Add (int keyNumber, object key, object val);

      public abstract int Count { get; }

      public abstract IFunctionalIntMap Remove (int keyNumber);

      internal abstract void AddKeys (ArrayList list);

      public void Visit (VisitMapPair visitor) { this.VisitTree(visitor); }

      internal abstract VisitStatus VisitTree (VisitMapPair visitor); 

      internal abstract void AppendToBuffer (System.Text.StringBuilder buffer);

      protected abstract int KeyNumber { get; }

      internal abstract void Dump (System.IO.TextWriter tw, string prefix);

      public void Dump (System.IO.TextWriter tw) { this.Dump(tw, ""); }

      public ICollection/*<object>*/ Keys 
      {
        // If gathering a list is too expensive, we can change this one day
        // That's why I made the return type IEnumerable rather than ICollection. RD
        get 
        { 
          ArrayList list = new ArrayList();
          this.AddKeys(list);
          return list;
        } 
      }

      public override string ToString ()
      {
        System.Text.StringBuilder buffer = new System.Text.StringBuilder();
        buffer.Append("[ ");
        this.AppendToBuffer(buffer);
        buffer.Append("]");
        return buffer.ToString();
      }

      protected static int LowestBit (int x) { return x & -x; }
      protected static int BranchingBit (int p0, int p1) { return LowestBit(p0 ^ p1); }
      protected static int MaskBits (int k, int m) { return k & (m-1); }
      protected static bool ZeroBit (int k, int m) { return (k & m) == 0; }
      protected static bool MatchPrefix (int k, int p, int m) { return MaskBits(k,m) == p; }


      protected static PatriciaTree Join (PatriciaTree t0, PatriciaTree t1)
      {
        int p0 = t0.KeyNumber, p1 = t1.KeyNumber;
        int m = BranchingBit(p0, p1);
        return ZeroBit(p0, m) ?
          new Branch(MaskBits(p0,m), m, t0, t1) :
          new Branch(MaskBits(p0,m), m, t1, t0);
      }
    }



    private class Empty : PatriciaTree
    {
      public static readonly Empty E = new Empty();

      protected override int KeyNumber { get { throw new System.NotImplementedException(); } }

      public override object Lookup (int key) { return null; }

      public override IFunctionalIntMap Add (int keyNumber, object key, object val) 
      {  
        return new Leaf(keyNumber, key, val); 
      }

      public override IFunctionalIntMap Remove (int keyNumber) { return this; }

      public override int Count { get { return 0; } }

      internal override void AddKeys (ArrayList list) { }

      internal override VisitStatus VisitTree (VisitMapPair visitor) { return VisitStatus.ContinueVisit; }

      internal override void AppendToBuffer (System.Text.StringBuilder buffer) { }

      internal override void Dump (System.IO.TextWriter tw, string prefix) { tw.WriteLine(prefix + "<Empty/>"); }
    }



    private class Leaf : PatriciaTree
    {
      public readonly int UniqueId;
      public readonly object Key;
      public readonly object Value;

      public Leaf (int k, object key, object val) { this.Key = key; this.UniqueId = k; this.Value = val; }

      protected override int KeyNumber { get { return this.UniqueId; } }

      public override object Lookup (int key) 
      { 
        return key == this.UniqueId ? this.Value : null; 
      }

      public override IFunctionalIntMap Add (int keyNumber, object key, object val) 
      { 
        int thisUniqueId = this.UniqueId;
        return (keyNumber == thisUniqueId) ? 
          new Leaf(keyNumber, key, val) : 
          Join(new Leaf(keyNumber, key, val), this); 
      }

      public override IFunctionalIntMap Remove (int keyNumber) 
      { 
        return keyNumber == this.UniqueId ? (IFunctionalIntMap)Empty.E : (IFunctionalIntMap)this; 
      }

      public override int Count { get { return 1; } }

      internal override void AddKeys (ArrayList list) { list.Add(this.Key); }

      internal override VisitStatus VisitTree (VisitMapPair visitor) { return visitor(this.Key, this.Value); }

      internal override void AppendToBuffer (System.Text.StringBuilder buffer) 
      { 
        buffer.AppendFormat("{0}->{1} ", this.Key, this.Value);
      }

      internal override void Dump (System.IO.TextWriter tw, string prefix) 
      { 
        tw.WriteLine(prefix + "<Leaf KeyInt={1} Key='{0}' Value='{2}'/>", Key, UniqueId, Value); 
      }
    }



    private class Branch : PatriciaTree
    {
      public readonly int Prefix;
      public readonly int Mask;
      public readonly PatriciaTree Left, Right;
      private readonly int count;

      public Branch (int prefix, int mask, PatriciaTree left, PatriciaTree right) 
      { 
        this.Prefix = prefix;
        this.Mask = mask; 
        this.Left = left; 
        this.Right = right; 
        this.count = left.Count + right.Count;
      }

      protected override int KeyNumber { get { return this.Prefix; } }

      public override object Lookup (int key) 
      { 
        if (ZeroBit(key, this.Mask))
          return this.Left.Lookup(key); 
        else
          return this.Right.Lookup(key); 
      }

      public override IFunctionalIntMap Add (int keyNumber, object key, object val) 
      { 
        if (MatchPrefix(keyNumber, this.Prefix, this.Mask))
        {
          if (ZeroBit(keyNumber, this.Mask))
          {
            return new Branch(this.Prefix, this.Mask, 
              (PatriciaTree) this.Left.Add(keyNumber, key, val), this.Right);
          }
          else
          {
            return new Branch(this.Prefix, this.Mask, this.Left, 
              (PatriciaTree) this.Right.Add(keyNumber, key, val));
          }
        }
        else
          return Join(new Leaf(keyNumber, key, val), this);
      }

      public override IFunctionalIntMap Remove (int keyNumber)
      {
        if (ZeroBit(keyNumber, this.Mask))
        {
          PatriciaTree newLeft = this.Left.Remove(keyNumber) as PatriciaTree;
          if (newLeft is Empty)
            return this.Right;
          return Join(newLeft, this.Right);
        }
        else
        {
          PatriciaTree newRight = this.Right.Remove(keyNumber) as PatriciaTree;
          if (newRight is Empty)
            return this.Left;
          return Join(this.Left, newRight);
        }
      }

      public override int Count { get { return this.count; } }

      internal override void AddKeys (ArrayList list) 
      { 
        this.Left.AddKeys(list);
        this.Right.AddKeys(list);
      }


      internal override VisitStatus VisitTree (VisitMapPair visitor) 
      { 
        if (this.Left.VisitTree(visitor) == VisitStatus.StopVisit)
          return VisitStatus.StopVisit;

        return this.Right.VisitTree(visitor);
      }


      internal override void AppendToBuffer (System.Text.StringBuilder buffer) 
      { 
        this.Left.AppendToBuffer(buffer);
        this.Right.AppendToBuffer(buffer);
      }

      internal override void Dump (System.IO.TextWriter tw, string prefix) 
      { 
        tw.WriteLine(prefix + "<Branch Prefix={0} Mask={1}>", Prefix, Mask); 
        this.Left.Dump(tw, prefix + "  ");
        this.Right.Dump(tw, prefix + "  ");
        tw.WriteLine(prefix + "</Branch>");
      }
    }


  }


  public class FunctionalSet : IFunctionalSet {
    IFunctionalIntMap fimap;

    private FunctionalSet(IFunctionalIntMap map) {
      this.fimap = map;
    }

    public static FunctionalSet Empty = new FunctionalSet(FunctionalIntMap.EmptyMap);

    #region IFunctionalSet Members

    public bool this[IUniqueKey key] {
      get {
        return this.fimap[key.UniqueId] != null;
      }
    }

    public IFunctionalSet Add(IUniqueKey key) {
      return new FunctionalSet(this.fimap.Add(key.UniqueId, key, Empty)); // Empty is used as any dummy value here
    }

    public IFunctionalSet Remove(IUniqueKey key) {
      return new FunctionalSet(this.fimap.Remove(key.UniqueId));
    }

    public ICollection Elements {
      get {
        return this.fimap.Keys;
      }
    }

    public int Count {
      get {
        return this.fimap.Count;
      }
    }

    public void Dump(TextWriter tw) {
      this.fimap.Dump(tw);
    }

    #endregion
  }
  


  public class FunctionalMap : IFunctionalMap {
    IFunctionalIntMap fimap;

    private FunctionalMap(IFunctionalIntMap map) {
      this.fimap = map;
    }

    public static FunctionalMap Empty = new FunctionalMap(FunctionalIntMap.EmptyMap);

    #region IFunctionalMap Members

    public object this[IUniqueKey key] {
      get {
        return this.fimap[key.UniqueId];
      }
    }

    public IFunctionalMap Add(IUniqueKey key, object val) {
      return new FunctionalMap(this.fimap.Add(key.UniqueId, key, val));
    }

    public IFunctionalMap Remove(IUniqueKey key) {
      return new FunctionalMap(this.fimap.Remove(key.UniqueId));
    }

    public ICollection Keys {
      get {
        return this.fimap.Keys;
      }
    }

    public void Visit(VisitMapPair visitor) {
      this.fimap.Visit(visitor);
    }

    public int Count {
      get {
        return this.fimap.Count;
      }
    }

    public void Dump(TextWriter tw) {
      this.fimap.Dump(tw);
    }

    #endregion
  }
  
  public interface ISet : ICollection, ICopyable
  {
    /// <summary>
    /// Checks whether a given element is part of <c>this</c> set.
    /// </summary>
    /// <param name="elem">element searched into the set</param>
    /// <returns><c>true</c> if <c>elem</c> is in the set, <c>false</c> otherwise</returns>
    bool Contains(Object o);

  }


  /// <summary>
  /// Interface for the set abstraction: collection of distinct elements.
  /// </summary>
  public interface IMutableSet: ISet, ICloneable
  {
    /// <summary>
    /// Adds an element to <c>this</c> set.
    /// </summary>
    /// <param name="elem">element to add</param>
    /// <returns><c>true</c> if <c>this</c> set was modified as a result of this operation</returns>
    /// 
    bool Add(Object o);

    /// <summary>
    /// Removes an element from <c>this</c> set. 
    /// </summary>
    /// <param name="elem"></param>
    /// <returns><c>true</c> if <c>this</c> set was modified as a result of this operation</returns>
    bool Remove(Object o);

    /// <summary>
    /// Adds several elements from <c>this</c> set.
    /// </summary>
    /// <param name="eable"><c>IEnumerable</c> that contains the elements to be added</param>
    /// <returns><c>true</c> if <c>this</c> set was modified as a result of this operation</returns>
    bool AddAll(IEnumerable eable);

    /// <summary>
    /// Removes several elements from <c>this</c> set.
    /// </summary>
    /// <param name="eable"><c>IEnumerable</c> containing the elements to be removed</param>
    /// <returns><c>true</c> if <c>this</c> set was modified as a result of this operation</returns>
    bool RemoveAll(IEnumerable eable);

    /// <summary>
    /// Deletes all the elements of <c>this</c> set. As a result the <c>Count</c> property will be <c>0</c>.
    /// </summary>
    /// <returns><c>true</c> if <c>this</c> set was modified as a result of this operation</returns>
    bool Clear();

  }


  public abstract class Set
  {
    [return: Microsoft.Contracts.NotNull]
    public static ISet Difference(IEnumerable a, ISet b)
    {
      IMutableSet diff = new HashSet();
      foreach(object elem in a)
        if(!b.Contains(elem))
          diff.Add(elem);
      return diff;
    }


    [return: Microsoft.Contracts.NotNull]
    public static ISet Union(ISet a, ISet b)
    {
      IMutableSet union = new HashSet(a);
      union.AddAll(b);
      return union;
    }


    [return: Microsoft.Contracts.NotNull]
    public static ISet Intersect(ISet a, ISet b) 
    {
      IMutableSet inter = new HashSet();

      if (a.Count < b.Count) 
      {
        ISet c = a;
        a = b;
        b = c;
      }
      foreach(object elem in a) 
      {
        if (b.Contains(elem)) 
        {
          inter.Add(elem);
        }
      }	
      return inter;
    }

		
    /// <summary>
    /// Returns null if A included in B. Otherwise, returns an element in
    /// A that is not in B.
    /// </summary>
    public static object NonSubsetWitness(ISet a, ISet b)
    {
      foreach(object elem in a) 
      {
        if (! b.Contains(elem) ) 
        {
          return elem;
        }
      }
      return null;
    }


    public delegate bool SetFilter(object obj);

    [return: Microsoft.Contracts.NotNull]
    public static ISet Filter(ISet a, SetFilter filter) 
    {
      IMutableSet inter = new HashSet();

      foreach(object elem in a) 
      {
        if (filter(elem))
        {
          inter.Add(elem);
        }
      }	
      return inter;
    }


    public static readonly ISet Empty = new ArraySet(0);
  }



  public abstract class AbstrSet: IMutableSet
  {
    public abstract bool Add(object elem);
    public virtual bool AddAll(IEnumerable eable) 
    {
      bool modified = false;
      foreach(object elem in eable)
        if(elem!=null)
          if(Add(elem)) modified = true;
      return modified;
    }

    public abstract bool Remove(object elem);

    public virtual bool RemoveAll(IEnumerable eable)
    {   
      ISet iset = eable as ISet;
      if((iset != null) && (iset.Count > 2 * this.Count)) 
      {
        // optimized code for the special case when eable is a large ISet
        ArrayList to_remove = new ArrayList();
        foreach(object elem in this)
          if(iset.Contains(elem))
            to_remove.Add(elem);
        if(to_remove.Count != 0)
        {
          foreach(object elem in to_remove)
            this.Remove(elem);
          return true;
        }
        return false;
      }
			
      bool modified = false;
      foreach(object elem in eable)
        if(Remove(elem)) modified = true;
      return modified;
    }			


    public abstract bool Contains(object elem);
    public abstract bool Clear();
    public abstract int Count { get; }


    public abstract IEnumerator GetEnumerator();

    public virtual void CopyTo(Array array, int index) 
    {
      foreach(object o in this) 
        array.SetValue(o, index++);
    }


    public override string ToString()
    {
      return DataStructUtil.IEnum2String(this, null);
    }

    public override int GetHashCode()
    {
      throw new InvalidOperationException("GetHashCode is unimplemented");
    }

    public override bool Equals(object o)
    {
      ISet iset2 = o as ISet;
      // if o is not an ISet, is a set with a different number of
      // elements, obviously o != this 
      if(	(iset2 == null) ||
        (iset2.Count != this.Count))
        return false;
      foreach(object elem in this)
        if(!iset2.Contains(elem))
          return false;
      return true;
    }

    public virtual bool IsSynchronized { get { return false; } }
		
    public virtual object SyncRoot { get { return this; } }

    public abstract object Clone();
    public abstract object Copy();
  }



  public class ArraySet : AbstrSet
  {
    public ArraySet(int initial_size)
    {
      array = new object[initial_size];
    }

    public ArraySet(object singleton) 
    {
      array = new object[] {singleton};
      count = 1;
    }

    public ArraySet() : this(5) { }

    private object[] array;
    private int count = 0;

    public override bool Add(object elem)
    {
      for(int i = 0; i < count; i++)
        if(elem.Equals(array[i]))
          return false;
      if(count == array.Length)
      {
        object[] array2 = new object[count*2];
        for(int i = 0; i < count; i++)
          array2[i] = array[i];
        array = array2;
      }
      array[count++] = elem;
      return true;
    }

    public override bool Remove(object elem)
    {
      for(int i = 0; i < count; i++)
        if(elem.Equals(array[i]))
        {
          if(i < count-1)
            array[i] = array[count-1];
          // no memory leaks
          array[count-1] = null;
          count--;
          return true;
        }
      return false;
    }

    public override bool Contains(object elem)
    {
      for(int i = 0; i < count; i++)
        if(elem.Equals(array[i]))
          return true;
      return false;
    }

    public override bool Clear()
    {
      bool result = count > 0;
      count = 0;
      return result;
    }

    public override int Count { get { return count; } }

    public override IEnumerator GetEnumerator()
    {
      return new ArrayEnumerator(array, count);
    }

    public override object Clone()
    {
      ArraySet set2 = (ArraySet) this.MemberwiseClone();
      set2.array = (object[]) this.array.Clone();
      return set2;
    }

    public override object Copy()
    {
      ArraySet set2 = (ArraySet) this.MemberwiseClone();
      set2.array = (object[]) this.array.Clone();
      for(int i = 0; i < set2.count; i++)
      {
        ICopyable elem = set2.array[i] as ICopyable;
        if(elem != null)
          set2.array[i] = elem.Copy();
      }
      return set2;
    }
  }



  public class ImmutableSetWrapper: IMutableSet
  {
    private ISet real_set;

    public ImmutableSetWrapper(ISet s)
    {
      real_set = s;
    }

    public bool Add(object key)
    {
      throw new ModifyImmutableCollException();
    }

		
    public bool AddAll(IEnumerable keys)
    {
      throw new ModifyImmutableCollException();
    }


    public bool Remove(object key)
    {
      throw new ModifyImmutableCollException();
    }


    public bool RemoveAll(IEnumerable keys)
    {
      throw new ModifyImmutableCollException();
    }


    public bool Clear()
    {
      throw new ModifyImmutableCollException();
    }
		

    public object Clone()
    {
      // memberwise clone is enough because no destructive operations
      // are allowed on this kind of objects.
      return this.MemberwiseClone();
    }

    public bool Contains(object key)
    {
      return real_set.Contains(key);
    }

    public IEnumerator GetEnumerator()
    {
      return real_set.GetEnumerator();
    }

    public override int GetHashCode()
    {
      return real_set.GetHashCode();
    }

    public int Count { get { return real_set.Count; } }

    void ICollection.CopyTo(Array target, int index) 
    {
      this.real_set.CopyTo(target, index);
    }

    bool ICollection.IsSynchronized 
    {
      get { return this.real_set.IsSynchronized; }
    }

    object ICollection.SyncRoot 
    {
      get { return this.real_set.SyncRoot; }
    }

    public object Copy()
    {
      ImmutableSetWrapper copy = (ImmutableSetWrapper) this.MemberwiseClone();
      // ISet implements ICopyable so we always do a deep copy of real_set
      copy.real_set = (ISet) this.real_set.Copy();
      return copy;
    }


  }


  public class ModifyImmutableCollException: SystemException
  {
    public ModifyImmutableCollException():
      base("Attempt to modify an immutable collection") {}
  }




  /// <summary>
  /// uses trivial hashtable as its set implementation
  /// </summary>
  public class NodeSet : AbstrSet 
  {
    private int count = 0;
    private TrivialHashtable values = new TrivialHashtable();
    private ArrayList keys = new ArrayList();

    public override bool Add(object elem)
    {
      Node node = (Node)elem;
      if (values[node.UniqueKey] != null) 
      {
        // already present
        return false;
      }
      values[node.UniqueKey] = node;
      keys.Add(node);
      this.count++;
      return true;
    }


    public override bool Clear()
    {
      if (this.count > 0) 
      {
        this.count = 0;
        this.values = new TrivialHashtable();
        this.keys = new ArrayList();
        return true;
      }
      return false;
    }

    public override object Clone()
    {
      NodeSet copy = new NodeSet();
      foreach (object o in this) 
      {
        copy.Add(o);
      }
      return copy;
    }

    public override bool Contains(object elem)
    {
      Node node = (Node)elem;

      return (this.values[node.UniqueKey] != null);
    }

    public override object Copy()
    {
      return this.Clone();
    }

    public override int Count
    {
      get
      {
        return this.count;
      }
    }

    public override bool Remove(object elem)
    {
      Node node = (Node)elem;
      if (this.values[node.UniqueKey] != null) 
      {
        this.values[node.UniqueKey] = null;
        this.count--;
        return true;
      }
      return false;
    }


    /// <summary>
    /// Has to clean out the ArrayList, since it may contain stale keys.
    /// </summary>
    public override IEnumerator GetEnumerator()
    {
      object[] nodes = new object[this.count];
      int i=0;
      for (int j=0; j<this.keys.Count; j++) 
      {
        object node = this.keys[j];
        if (this.Contains(node)) 
        {
          nodes[i++] = node;
        }
      }
      return new ArrayEnumerator(nodes, this.count);
    }


  }


  /// <summary>
  /// Full implementation of the <c>ISet</c> interface, backed by a <c>Hashtable</c>.
  /// </summary>
  /// <remarks>
  /// As each <c>HashSet</c> is backed by a
  /// <see cref="System.Collections.Hashtable">Hashtable</see>, all requirements that
  /// apply for the <c>Hashtable</c> keys apply for the elements of a <c>HashSet</c>
  /// as well.
  ///
  /// <p>The <c>HashSet</c> class overrides the methods
  /// <see cref="GetHashCode">GetHashCode</see> and <see cref="System.Object.Equals(System.Object)">Equals</see>
  /// (inherited from <see cref="System.Object">Object</see>) in order to provide
  /// structural equality:
  /// two sets are equal iff they contain the same elements (where the semantics of "same"
  /// is defined by the <c>Equals</c> method of those objects).  You can put HashSets into
  /// HashSets; however, to avoid infinite loops, you should never insert a <c>HashSet</c>
  /// into itself.
  /// The hashcode of a <c>HashSet</c> is defined as the "xor" of the hashcodes of the set
  /// elements. 
  /// </p>
  /// 
  /// <p>
  /// The <c>GetHashCode</c> function of a <c>HashSet</c> executes in <c>O(1)</c> time:
  /// the hashcode is dynamically updated after each operation that modifies the set.
  /// If the hashcode functions used for all the other involved objects is good and
  /// is computed in <c>O(1)</c> time, one element addition and removal execute in
  /// <c>O(1)</c> time; <c>Equals</c> works in time linear to the number of elements of
  /// <c>this</c> set.
  /// </p> 
  /// </remarks>
  public class HashSet: AbstrSet
  {
    // the Hashtable that backs up the implementation of this HashSet
    // an element is in the set if it's a key in the Hashtable
    protected Hashtable hash;

    // all keys are associated with the bogus value VALUE
    protected static object VALUE = new object();

    /// <summary>
    /// Constructs an empty <c>HashSet</c>.
    /// </summary>
    public HashSet()
    {
      init(0);
    }

    public HashSet(int initialsize)
    {
      init(initialsize);
    }

    // does the real constructor job; called by constructor and Copy
    private void init(int initialsize)
    {
      if (initialsize != 0) 
      {
        hash = new Hashtable(initialsize);
      }
      else 
      {
        hash = new Hashtable();
      }
      set_hash_code = 0;
    }
		
    /// <summary>
    /// Constructs a <c>HashSet</c> initialized to contain all
    /// elements from an <c>IEnumerable</c>.
    /// </summary>
    public HashSet(IEnumerable eable) : this()
    {
      AddAll(eable);
    }

    public override bool Contains(object elem)
    {
      return hash.ContainsKey(elem);
    }

    public override bool Add(object elem) 
    {
      if(elem == null)
        throw new ApplicationException("null set element");

      // element already present in the set
      if(hash.ContainsKey(elem)) return false;
      // new element!
      hash[elem] = VALUE;
      set_hash_code ^= elem.GetHashCode();
      return true;
    }

    public override bool Remove(object elem)
    {
      if(!hash.ContainsKey(elem)) return false;
      hash.Remove(elem);
      // a^b^b = a; we eliminate the influence of "elem" on the
      // hash code for the entire set
      set_hash_code ^= elem.GetHashCode();
      return true;
    }

    public override bool Clear()
    {
      set_hash_code = 0;
      bool result = (this.Count != 0);
      hash.Clear();
      return result;
    }

    public override int Count { get { return hash.Count; } }

    protected class HashSetEnumerator: IEnumerator 
    {
      HashSet hash_set = null;
      IEnumerator hash_e = null;

      public HashSetEnumerator(HashSet hash_set) 
      {
        this.hash_set  = hash_set;
        this.hash_e    = hash_set.hash.GetEnumerator();
      }
			
      public object Current
      {
        get
        {
          return ((DictionaryEntry) hash_e.Current).Key;
        }
      }

      public bool MoveNext() 
      {
        return hash_e.MoveNext();
      }

      public void Reset() 
      {
        hash_e.Reset();
      }
    }

    [return: Microsoft.Contracts.NotNull]
    public override IEnumerator GetEnumerator() 
    {
      return new HashSetEnumerator(this);
    }
		
    public override int GetHashCode()
    {
      return set_hash_code;
    }
    // hash_code of this set; dynamically maintained after each
    // operation on this set
    protected int set_hash_code;

    public override object Clone()
    {
      HashSet copy = (HashSet) this.MemberwiseClone();
      //Fugue.AssertNotAliased(copy);
      copy.init(this.Count);
      foreach(object elem in this)
        copy.Add(elem);
      return copy;
    }

    public override object Copy()
    {
      HashSet copy = (HashSet) this.MemberwiseClone();
      //Fugue.AssertNotAliased(copy);
      copy.init(this.Count);
      foreach(object elem in this)
        copy.Add((elem is ICopyable) ? ((ICopyable) elem).Copy() : elem);

      return copy;
    }
  }




  public interface IRelation 
  {
    bool ContainsKey(object key);

    bool Contains(object key, object value);

    ICollection GetKeys();

    ISet GetValues(object key);

    IRelation Reverse();
  }


  public interface IMutableRelation : IRelation, ICloneable, ICopyable
  {
    bool Add(object key, object value);

    bool AddAll(object key, IEnumerable values);

    /// <summary>
    /// Adds an entire relation to <d>this</d> relation.
    /// </summary>
    /// <param name="relation">Relation that is unioned with this relation.</param>
    /// <returns><c>true</c> iff <c>this</c> relation changed.</returns>
    bool AddRelation(IRelation relation);

    bool Remove(object key, object value);
    bool RemoveAll(object key, IEnumerable values);

    bool RemoveKey(object key);
    bool RemoveSeveralKeys(IEnumerable keys); 

    bool RemoveSeveralValues(IEnumerable values);

    new
      IMutableRelation Reverse();

  }


  /// <summary>
  /// Full <c>IMutableRelation</c> implementation.
  /// </summary>
  public class Relation: IMutableRelation
  {
    /// <summary>
    /// Full power relation constructor that allows you to finely tune the memory consumption.
    /// Internally, a relation is a dictionary that assigns to each key the set of values that are
    /// in relation with it.  This constructor allows you to specify the dictionary and the set
    /// factory.
    /// </summary>
    /// <param name="dict_fact">Dictionary factory used to construct the underlying dictionary.</param>
    /// <param name="set_fact">Set factory used to construct the set that will store the values
    /// associated with each key.</param>
    public Relation(DDictionaryFactory dict_fact, DSetFactory set_fact)
    {
      this.dict_fact = dict_fact;
      this.set_fact  = set_fact;
      init();
    }

    /// <summary>
    /// Default constructor.  Uses the default factory for dictionaries (i.e., equiv. to new Hashtable())
    /// and sets (i.e., equiv. to new HashSet()).
    /// </summary>
    public Relation() :
      this(DataStructUtil.DefaultDictionaryFactory, DataStructUtil.DefaultSetFactory) {}

    private readonly DDictionaryFactory dict_fact;
    private readonly DSetFactory        set_fact;

    // Method doing the actual initialization. Called by both the constructor and the Copy method.
    private void init()
    {
      hash = dict_fact();
    }

    // underlying structure that stores the information attached with this relation
    private IDictionary hash/*<object, ISet<object>>*/;

    public virtual bool Add(object key, object value)
    {
      return get_set_for_add(key).Add(value);
    }

    public virtual bool AddAll(object key, IEnumerable values)
    {
      return get_set_for_add(key).AddAll(values);
    }

    public virtual bool AddRelation(IRelation relation)
    {
      if(relation == null) return false;
      bool changed = false;
      foreach(object key in relation.GetKeys())
        if(this.AddAll(key, relation.GetValues(key)))
          changed = true;
      return changed;
    }

    private IMutableSet get_set_for_add(object key)
    {
      IMutableSet s = (IMutableSet) hash[key];
      if(s == null)
      {
        s = set_fact();
        hash[key] = s;
      }
      return s;
    }

    public virtual ISet GetValues(object key)
    {
      ISet s = (ISet) hash[key];
      if(s == null)
        s = DataStructUtil.EMPTY_SET;
      else
        s = new ImmutableSetWrapper(s);
      return s;
    }

    public virtual bool ContainsKey(object key)
    {
      return hash.Contains(key);
    }

    public virtual bool Contains(object key, object value)
    {
      return ((ISet) GetValues(key)).Contains(value);
    }

    public virtual bool Remove(object key, object value)
    {
      IMutableSet s = (IMutableSet) hash[key];
      if(s == null)
        return false;

      bool result = s.Remove(value);
      if(s.Count == 0)
        hash.Remove(key);

      return result;
    }

    public virtual bool RemoveAll(object key, IEnumerable values)
    {
      IMutableSet s = (IMutableSet) hash[key];
      if(s == null)
        return false;

      bool result = s.RemoveAll(values);
      if(s.Count == 0)
        hash.Remove(key);

      return result;
    }


    public virtual bool RemoveKey(object key)
    {
      if(hash.Contains(key))
      {
        hash.Remove(key);
        return true;
      }
      return false;
    }

    public virtual bool RemoveSeveralKeys(IEnumerable keys)
    {
      ISet iset_keys = keys as ISet;
      if(iset_keys != null)
      {
        ICollection keys_of_this = this.GetKeys();
        if(iset_keys.Count > 2 * keys_of_this.Count)
        {
          // optimized code for the special case when "keys" is a large ISet
          // UGLY code! comodif. + IEnumerator has no remove method
          ArrayList to_remove = new ArrayList();
          foreach(object key in keys_of_this)
            // set membership is O(1) :)
            if(iset_keys.Contains(key))
              to_remove.Add(key);
          if(to_remove.Count > 0)
          {
            foreach(object key in to_remove)
              hash.Remove(key);
            return true;
          }
          return false;
        }

      }

      bool modified = false;
      foreach(object key in keys)
        if(hash.Contains(key))
        {
          hash.Remove(key);
          modified = true;
        }
      return modified;
    }


    public virtual bool RemoveSeveralValues(IEnumerable values)
    {
      bool modified = false;

      // the following code is inneficient ... this is due to:
      // 1. the need to avoid Comodification exceptions
      // 2. the lack of a remove method in IEnumerator()
      ArrayList keys = new ArrayList(GetKeys());
      foreach(object key in keys)
      {
        IMutableSet s = (IMutableSet) hash[key];
        if(s.RemoveAll(values))
        {
          modified = true;
          if(s.Count == 0)
            hash.Remove(key);
        }
      }

      return modified;
    }

		
    public virtual ICollection GetKeys()
    {
      return hash.Keys;
    }

    public virtual IMutableRelation Reverse()
    {
      IMutableRelation reverse = new Relation();
      foreach(object key in GetKeys())
        foreach(object value in GetValues(key))
          reverse.Add(value, key);
      return reverse;
    }

    IRelation IRelation.Reverse() 
    {
      return this.Reverse();
    }


    /// <summary>
    /// "Shallow" copy of a relation.  Produces an independent copy of <c>this</c> <c>Relation</c>.
    /// The keys and values are not duplicated.  Operations on the
    /// resulting <c>Relation</c> and on <c>this</c> <c>Relation</c> don't interact.
    /// </summary>
    /// <returns>An independent copy of <c>this</c> Relation.</returns>
    public virtual object Clone()
    {
      Relation copy = (Relation) this.MemberwiseClone();
      //Fugue.AssertNotAliased(copy);

      copy.init();
      foreach(object key in this.GetKeys())
        foreach(object val in this.GetValues(key))
          copy.Add(key, val);
      return copy;
    }

    /// <summary>
    /// Deep copy of a relation.  Produces an independent copy of <c>this</c> <c>Relation</c>,
    /// in which even the keys and values are duplicated (using deep copy) if they implement
    /// the ICopyable interface.  Operations on the resulting <c>Relation</c> and on <c>this</c>
    /// <c>Relation</c> don't interact.
    /// </summary>
    /// <returns>A really deep copy of <c>this</c> <c>Relation</c>.</returns>
    public virtual object Copy()
    {
      Relation copy = (Relation) this.MemberwiseClone();
      //Fugue.AssertNotAliased(copy);
      copy.init();
      foreach(object key in this.GetKeys())
      {
        // deep copy of the key, if possible
        object key2 = (key is ICopyable) ? ((ICopyable) key).Copy() : key;
        foreach(object val in this.GetValues(key))
        {
          // deep copy of the value, if possible
          object val2 = (val is ICopyable) ? ((ICopyable) val).Copy() : val;
          copy.Add(key2, val2);
        }
      }
      return copy;
    }


    public override string ToString()
    {
      StringBuilder sb = new StringBuilder();
      sb.Append("[");
      foreach(object key in this.GetKeys())
      {
        sb.Append("\n  ");
        sb.Append(key);
        sb.Append(" -> " );
        sb.Append(this.GetValues(key));
      }
      sb.Append("]");
      return sb.ToString();
    }

    public static Hashtable/*<Key,Value[]>*/ Compact(IRelation/*<Key,Value>*/ irel, Type value_type) 
    {
      Hashtable hash = new Hashtable();
      foreach(object key in irel.GetKeys()) 
      {
        ISet vals = irel.GetValues(key);
        if(vals.Count == 0)
          continue;
        System.Array array_vals = System.Array.CreateInstance(value_type, vals.Count);
        vals.CopyTo(array_vals, 0);
        if (typeof(Node).IsAssignableFrom(value_type)) 
        {
          Array.Sort(array_vals, DataStructUtil.NodeComparer);
        }
        hash.Add(key, array_vals);
      }
      return hash;
    }
  }


  public class BlockRelation : Relation 
  {
    public BlockRelation() : base(DataStructUtil.DefaultDictionaryFactory, DataStructUtil.NodeSetFactory)
    {
    }
  }

  /// <summary>
  /// Returns null if object should not be returned, otherwise, returns object.
  /// Can thus change objects.
  /// </summary>
  public delegate object EnumeratorFilter(object elem, object context);

  /// <summary>
  /// "Glues" together two <c>IEnumerable</c> objects in a single view.
  /// </summary>
  public class CompoundEnumerable: IEnumerable 
  {

    /// <summary>
    /// Construct an enumerable that enumerators over ieable1, then ieable2. Each element
    /// is passed to the filter which can decide if the element should be returned by
    /// the enumerator or not. The filter can also change the element (map).
    /// </summary>
    /// <param name="filter">can be null</param>
    /// <param name="context">passed to filter</param>
    public CompoundEnumerable (
      IEnumerable ieable1,
      IEnumerable ieable2,
      EnumeratorFilter filter, 
      object context) 
    {
      this.ieable1 = ieable1;
      this.ieable2 = ieable2;
      this.filter = filter;
      this.context = context;
    }

    public CompoundEnumerable(IEnumerable ieable1, IEnumerable ieable2) : this(ieable1, ieable2, null, null)
    {
    }

    private IEnumerable ieable1;
    private IEnumerable ieable2;
    private EnumeratorFilter filter;
    private object context;

    public virtual IEnumerator GetEnumerator() 
    {
      return 
        new MultiEnumerator(ieable1.GetEnumerator(), ieable2.GetEnumerator(), filter, context);
    }
  }

  /// <summary>
  /// Serial composition of two enumerators.  Enumerating with a
  /// multi-enumerator is equivalent to enumerating with the first enumerator,
  /// and next with the second one.  Implements the full <c>IEnumerable</c>
  /// interface.  Aliases to the enumerators are sequentially composed are
  /// internally stored and used by the encapsulating multi-enumerator.
  /// </summary>
  public class MultiEnumerator: IEnumerator 
  {
    /// <summary>
    /// Creates a <c>MultiEnumerator</c> that serially chains the two
    /// enumerators passed as arguments.
    /// </summary>
    public MultiEnumerator(IEnumerator ie1, IEnumerator ie2, EnumeratorFilter filter, object context) 
    {
      this.filter = filter;
      this.context = context;
      ies = new IEnumerator[]{ie1, ie2};
      ies[0].Reset();
      ies[1].Reset();
    }

    private EnumeratorFilter filter;
    private IEnumerator[] ies;
    private int current_ie;
    private object context;

    private object currentValue;

    public virtual object Current 
    {
      get 
      {
        if(current_ie >= ies.Length)
          throw new InvalidOperationException("exhausted enumerator");
        return
          this.currentValue;
      }
    }

    public virtual bool MoveNext() 
    {
      if(current_ie >= ies.Length)
        return false;
      while(current_ie < ies.Length) 
      {
        if(ies[current_ie].MoveNext()) 
        {
          object value = ies[current_ie].Current;
          if (this.filter != null) 
          {
            value = filter(value, this.context);
            if (value == null) // skip this one
            {
              continue;
            }
          }
          this.currentValue = value;
          return true;
        }
        ies[current_ie].Reset();
        current_ie++;                
      }
      return false;
    }

    public virtual void Reset() 
    {
      if(current_ie < ies.Length)
        ies[current_ie].Reset();
      current_ie = 0;
    }
  }





  
  /// <summary>
  /// Union enumerator over two <c>IEnumerable</c> objects. Each key is visited only once
  /// </summary>
  public class UnionEnumerable: IEnumerable 
  {

    public UnionEnumerable(IEnumerable ieable1, IEnumerable ieable2) 
      : this(new IEnumerable[]{ieable1, ieable2})
    {
    }

    public UnionEnumerable(IEnumerable/*<IEnumerable>*/ ienums) 
    {
      this.ienums = ienums;
    }

    private IEnumerable/*<IEnumerable>*/ ienums;

    public virtual IEnumerator GetEnumerator() 
    {
      return new UnionEnumerator(ienums);
    }
  }

  /// <summary>
  /// Union composition of two enumerators.  Enumerating with a
  /// multi-enumerator is equivalent to enumerating over the union of the elements in
  /// the first and second enumerator.
  /// </summary>
  public class UnionEnumerator: IEnumerator 
  {
    /// <summary>
    /// Creates a <c>UnionEnumerator</c> over both given enumerators.
    /// </summary>
    public UnionEnumerator(IEnumerable/*<IEnumerable>*/ ienums) 
    {
      ies = ienums;

      Reset();
    }

    public void Reset() 
    {
      ienumate = ies.GetEnumerator();

      // go to first enumerator.
      MoveEnumerator();

      seen = new HashSet();
    }

    private void MoveEnumerator() 
    {
      if (ienumate.MoveNext()) 
      {
        this.current_ie = ((IEnumerable)ienumate.Current).GetEnumerator();
      }
      else 
      {
        this.current_ie = null;
      }
    }

    public UnionEnumerator(IEnumerable ie1, IEnumerable ie2) : this(new IEnumerable[]{ie1,ie2})
    {
    }

    private HashSet seen;
    private IEnumerable/*<IEnumerable>*/ ies;
    private IEnumerator/*<IEnumerable>*/ ienumate;
    private IEnumerator/*<'a>*/ current_ie;

    public virtual object Current 
    {
      get 
      {
        if(current_ie == null)
          throw new InvalidOperationException("exhausted enumerator");
        return
          current_ie.Current;
      }
    }

    public virtual bool MoveNext() 
    {
      while(current_ie != null)
      {
        if (current_ie.MoveNext())
        {
          if (this.seen.Add(current_ie.Current)) 
          {
            return true;
          }
          // already seen
          continue;
        }
        // move to next ienumerable
        MoveEnumerator();
      }
      return false;
    }

  }



  public interface IWorkList: ICollection
  {
    bool Add(object o);
    bool AddAll(IEnumerable objs);
    bool IsEmpty();
    object Pull();
  }

  public abstract class AbstrWorkList: IWorkList
  {
    // set of worklist members - for quick membership testing
    protected IMutableSet elems = new HashSet();
    // collection of worklist elements - this provides the order
    protected abstract ICollection coll { get; }

    public abstract bool Add(object o);
    public virtual bool AddAll(IEnumerable objs)
    {
      bool result = false;
      foreach(object obj in objs)
        if(Add(obj)) result = true;
      return result;
    }

    public virtual bool IsEmpty()
    {
      return coll.Count == 0;
    }

    public abstract object Pull();
    public virtual IEnumerator GetEnumerator()
    {
      return coll.GetEnumerator();
    }

    public virtual int Count { get { return coll.Count; } }
    public virtual bool IsSynchronized { get { return false; } }
		
    public virtual object SyncRoot 
    {
      get { return this; } 
    }

    public virtual void CopyTo(Array array, int index)
    {
      coll.CopyTo(array, index);
    }
  }
	

  /// <summary>
  /// Stack-based implementation of IWorkList.
  /// </summary>
  public sealed class WorkStack: AbstrWorkList
  {
    private Stack stack;

    protected override ICollection coll
    {
      get
      {
        return stack;
      }
    }


    public WorkStack() 
    {
      this.stack = new Stack();
    }

    public override bool Add(object o)
    {
      if(!elems.Add(o)) return false;
      stack.Push(o);
      return true;
    }

    public override object Pull()
    {
      object result = stack.Pop();
      elems.Remove(result);
      return result;
    }

  }


  /// <summary>
  /// Queue-based implementation of IWorkList.
  /// </summary>
  public sealed class WorkList: AbstrWorkList
  {
    private Queue queue;

    protected override ICollection coll
    {
      get
      {
        return queue;
      }
    }

    public WorkList()
    {
      queue = new Queue();
    }

    public override bool Add(object o)
    {
      if(elems.Add(o))
      {
        queue.Enqueue(o);
        return true;
      }
      return false;
    }

    public override object Pull()
    {
      object o = queue.Dequeue();
      elems.Remove(o);
      return o;
    }
  }


  /// <summary>
  /// Returns 0 if x and y are equal, less than 0 if x is less than y and greater than 0 if x is greater than y
  /// </summary>
  public delegate int Compare(object x, object y);

  /// <summary>
  /// Implements a work list as a priority queue
  /// </summary>
  public class PriorityQueue : AbstrWorkList
  {
    protected ArrayList array = new ArrayList();

    Compare compare;

    public PriorityQueue(Compare comparer) 
    {
      this.compare = comparer;
    }


    // algorithm is written with array of base 1, so adjust here
    private object this[int index] 
    {
      get 
      { 
        return array[index-1];
      }

      set
      {
        array[index-1] = value;
      }
    }

    private bool GreaterThan(object o1, object o2) 
    {
      return this.compare(o1, o2) > 0;
    }

    private static int Left(int i) 
    {
      return 2*i;
    }

    private static int Right(int i)
    {
      return 2*i+1;
    }

    private static int Parent(int i) 
    {
      return i/2;
    }

    private int HeapSize 
    {
      get { return this.array.Count; } 
    }

    private void Heapify(int i) 
    {
      int l = Left(i);
      int r = Right(i);
      int largest;

      if (l <= HeapSize && GreaterThan(this[l], this[i]))
      {
        largest = l;
      }
      else 
      {
        largest = i;
      }
      if (r <= HeapSize && GreaterThan(this[r], this[largest]))
      {
        largest = r;
      }
      if (largest != i) 
      {
        object tmp = this[largest];
        this[largest] = this[i];
        this[i] = tmp;
        Heapify(largest);
      }
    }                           
 

    public override bool Add(object o)
    {
      if (elems.Add(o)) 
      {
        array.Add(o);
        int i = HeapSize;
        while (i > 1 && GreaterThan(o, this[Parent(i)])) 
        {
          this[i] = this[Parent(i)];
          i = Parent(i);
        }
        this[i] = o;

        return true;
      }
      return false;
    }


    protected override ICollection coll
    {
      get
      {
        return this.array;
      }
    }


    public override object Pull()
    {
      if (HeapSize < 1) throw new InvalidOperationException("priority queue is empty");

      object max = this[1];
      this[1] = this[HeapSize];
      array.RemoveAt(HeapSize-1); // remove last element.

      Heapify(1);
      elems.Remove(max);
      return max;
    }

  }







  /// <summary>
  /// Interface for navigating into a graph.
  /// </summary>
  public interface IGraphNavigator
  {
    /// <summary>
    /// Returns the nodes that can be reached from <c>node</c> by
    /// navigating one level along the graph edges.
    /// </summary>
    IEnumerable NextNodes (object node);
    /// <summary>
    /// Returns the nodes that can be reached from <c>node</c> by
    /// navigating one level AGAINST the graph edges (i.e., from edge
    /// target to the edge source).
    /// </summary>
    IEnumerable PreviousNodes (object node);
  }


  /// <summary>
  /// Navigator for the graph obtained by unioning two graphs.
  /// </summary>
  public class UnionGraphNavigator: IGraphNavigator
  {
    /// <summary>
    /// Constructs a navigator into a graph which is the union of two graphs
    /// (where the graphs are seen as edge sets).
    /// </summary>
    /// <param name="nav1">Navigator for the first graph.</param>
    /// <param name="nav2">Navigator for the second graph.</param>
    public UnionGraphNavigator(IGraphNavigator nav1, IGraphNavigator nav2)
    {
      this.nav1 = nav1;
      this.nav2 = nav2;
    }
    private IGraphNavigator nav1;
    private IGraphNavigator nav2;

    /// <summary>
    /// In a union graph, the list of successors of a node includes its successors in
    /// the first graph followed by its successors in the second graph.
    /// </summary>
    public virtual IEnumerable NextNodes (object node)
    {
      return new CompoundEnumerable(nav1.NextNodes(node), nav2.NextNodes(node));
    }

    /// <summary>
    /// In a union graph, the list of predecessors of a node includes the its predecessors in
    /// the first graph followed by its predecessors in the second graph.
    /// </summary>
    public virtual IEnumerable PreviousNodes (object node)
    {
      return new CompoundEnumerable(nav1.PreviousNodes(node), nav2.PreviousNodes(node));
    }
  }


  public abstract class ForwardOnlyGraphNavigator: IGraphNavigator
  {
    public abstract IEnumerable NextNodes (object node);

    public virtual IEnumerable PreviousNodes (object node)
    {
      throw new InvalidOperationException("should never be called!");
    }
  }

  /// <summary>
  /// Navigator for an inverse graph.  The successors (i.e., <c>NextNodes</c>)
  /// of a node are the predecessors of the node in the original graph.  Analogously
  /// for the predecessors.
  /// </summary>
  public class BackwardGraphNavigator: IGraphNavigator
  {
    private readonly IGraphNavigator navigator;

    /// <summary>
    /// Constructs a <c>BackwardGraphNavigator</c> that reverses an
    /// <c>IGraphNavigator</c>.
    /// </summary>
    /// <param name="navigator">The navigator that is reversed.</param>
    public BackwardGraphNavigator(IGraphNavigator navigator)
    {
      this.navigator = navigator;
    }

    public IEnumerable NextNodes (object node)
    {
      return navigator.PreviousNodes(node);
    }

    public IEnumerable PreviousNodes (object node)
    {
      return navigator.NextNodes(node);
    }
  }

	
  public class FilteredGraphNavigator : IGraphNavigator
  {

    IGraphNavigator graph;

    ISet nodes;

    /// <summary>
    /// Only nodes in given set are considered part of the graph.
    /// </summary>
    public FilteredGraphNavigator(ISet nodes, IGraphNavigator graph) 
    {
      this.graph = graph;
      this.nodes = nodes;
    }

    #region IGraphNavigator Members

    public IEnumerable NextNodes (object node)
    {
      return new FilterEnumerable(this.nodes, this.graph.NextNodes(node));
    }

    public IEnumerable PreviousNodes (object node)
    {
      return new FilterEnumerable(this.nodes, this.graph.PreviousNodes(node));
    }

    private class FilterEnumerable : IEnumerable
    {
      ISet nodes;
      IEnumerable edges;

      public FilterEnumerable(ISet nodes, IEnumerable edges) 
      {
        this.nodes = nodes;
        this.edges = edges;
      }

      public IEnumerator GetEnumerator() { return new FilterEnumerator(this.nodes, this.edges.GetEnumerator()); }


      private class FilterEnumerator : IEnumerator
      {
        ISet nodes;
        IEnumerator edges;

        public FilterEnumerator(ISet nodes, IEnumerator edges) 
        {
          this.nodes = nodes;
          this.edges = edges;
        }

        #region IEnumerator Members

        public void Reset()
        {
          this.edges.Reset();
        }

        public object Current
        {
          get
          {
            return this.edges.Current;
          }
        }

        public bool MoveNext()
        {
          while (this.edges.MoveNext()) 
          {
            if (this.nodes.Contains(this.Current)) 
            {
              return true;
            }
          }
          return false;
        }

        #endregion

      }
    }
    #endregion
  }


  public class MapBasedNavigator: IGraphNavigator
  {
    protected IMutableRelation n2next;
    protected IMutableRelation n2prev;

    public MapBasedNavigator (IMutableRelation nextRelation, IMutableRelation previousRelation) 
    {
      n2next = nextRelation; 
      n2prev = previousRelation;
    }

    public MapBasedNavigator (IMutableRelation nextRelation) 
      : this(nextRelation, nextRelation.Reverse())
    {
    }

    public virtual IEnumerable NextNodes (object node)
    {
      return n2next.GetValues(node);
    }

    public virtual IEnumerable PreviousNodes (object node)
    {
      return n2prev.GetValues(node);
    }		
  }


  public class GraphBuilder : MapBasedNavigator 
  {
    public GraphBuilder () 
      : base (new Relation(), new Relation())
    {
    }

    public IEnumerable Nodes 
    {
      get 
      { 
        return new UnionEnumerable(n2next.GetKeys(), n2prev.GetKeys()); 
      }
    }

    public void AddEdge(object from, object to) 
    {
      this.n2next.Add(from, to);
      this.n2prev.Add(to, from);
    }
  }


  /// <summary>
  /// Navigator in a component graph (an acyclic graph of ISCCs).
  /// </summary>
  public class SccNavigator: IGraphNavigator
  {
    public virtual IEnumerable NextNodes (object node)
    {
      return ((IStronglyConnectedComponent) node).NextComponents;
    }

    public virtual IEnumerable PreviousNodes (object node)
    {
      return ((IStronglyConnectedComponent) node).PreviousComponents;
    }
  }


  public class CyclicGraphException: Exception { }

  public delegate bool DNodePredicate (Object node);
  public delegate void DNodeVisitor (Object node);
  public delegate void DEdgeVisitor (Object from, Object to);

  public abstract class GraphUtil
  {
    public static ISet ReachableNodes (DSetFactory setfactory, IEnumerable roots, IGraphNavigator navigator, DNodePredicate avoid)
    {
      ReachableNodesData data = new ReachableNodesData(setfactory);
      SearchDepthFirst(roots, navigator, avoid, null,
        new DNodeVisitor(data.reachable_visitor), null);
      return data.all_nodes;
    }

    private class ReachableNodesData
    {
      public IMutableSet all_nodes;

      public ReachableNodesData(DSetFactory setfactory) 
      {
        this.all_nodes = setfactory();
      }

      public void reachable_visitor(object node)
      {
        all_nodes.Add(node);
      }
    }

    public static ISet ReachableNodes (DSetFactory setfactory, IEnumerable roots, IGraphNavigator navigator) 
    {
      return ReachableNodes(setfactory, roots, navigator, null);
    }


    /// <summary>
    /// Topologically sorts the graph rooted in <c>roots</c> and described by
    /// <c>nav</c>. Throws a <c>CyclicGraphException</c> if the graph contains
    /// a cycle. Otherwise, returns a topologically sorted list of the graph nodes. 
    /// The returned list is in ascending order: it starts with the nodes that don't
    /// have any out-arc (i.e., arcs going out of them) and ends with the nodes
    /// that don't have any in-arcs (i.e., arcs going into them).  If the navigator
    /// works in constant time, the topological sort works in time linear with the
    /// number of nodes plus the number of edges. 
    /// 
    /// </summary>
    public static IList TopologicallySortGraph (DSetFactory setfactory, IEnumerable roots, IGraphNavigator navigator)
    {
      TopSortData data = new TopSortData(setfactory);
      data.all_nodes.AddAll(ReachableNodes(setfactory, roots, navigator));
      if(data.all_nodes.Count == 0)
        return data.list;

      // find the real roots: those nodes with no arcs pointing to them
      data.real_roots.AddAll(roots);
      foreach(object node in data.all_nodes)
        foreach(object next_node in navigator.NextNodes(node))
          data.real_roots.Remove(next_node);
      // if there is no real root, we have a cycle
      if(data.real_roots.Count == 0)
        throw new CyclicGraphException();

      dfs(data.real_roots, navigator, null, null,
        new DNodeVisitor(data.sort_end_visitor));

#if NEVER
			// check for cyles
			IMutableSet seen = new HashSet();
			foreach(object node in data.list)
			{
				foreach(object next_node in navigator.NextNodes(node))
					// all arcs must go behind in the list, to already seen nodes
					if(!seen.Contains(next_node))
						throw new CyclicGraphException();
				seen.Add(node);
			}
#endif
      return data.list;
    }

    private class TopSortData
    {
      public ArrayList list  = new ArrayList();
      public IMutableSet all_nodes;
      public IMutableSet real_roots;
      public void sort_end_visitor(object node)
      {
        list.Add(node);
      }
      public TopSortData(DSetFactory setfactory) 
      {
        this.all_nodes = setfactory();
        this.real_roots = setfactory();
      }
    }


    /// <summary>
    /// Topologically sorts a component graph: a graph whose nodes are the
    /// strongly connected components of the original graph (such a graph is
    /// clearly acyclic). Calls the full-fledged TopSortComponentGraph with
    /// the standard <c>ISCCNavigator</c>.
    /// 
    /// </summary>
    /// <param name="roots">The set of the root SCCs, only the SCCs reachable
    /// from these roots will be considered by the topological sort.</param>
    /// <returns></returns>
    public static IList/*<IStronglyConnectedComponent>*/ 
      TopologicallySortComponentGraph(DSetFactory setfactory, IEnumerable/*<IStronglyConnectedComponent>*/ roots)
    {
      return TopologicallySortGraph(setfactory, roots, iscc_navigator);
    }
    // private navigator for TopSortComponentGraph
    private static SccNavigator iscc_navigator = new SccNavigator();

    /// <summary>
    /// DFS traversal of the (sub)graph rooted in a set of nodes.
    /// </summary>
    /// <param name="roots">Roots of the traversed subgraph. The subgraph
    /// rooted in the first root will be traversed in DFS order; next, if
    /// the second root wasn't reached yet, the subgraph rooted in it will
    /// be traversed in DFS order and so on. The order of
    /// the roots is given by the corresponding <c>IEnumerator</c>.</param>
    /// <param name="navigator">Navigator that describes the graph structure.</param>
    /// <param name="avoid">Encountered nodes that satisfy this predicate will be
    /// ignored by the DFS traversal (together with their attached arcs). <c>null</c>
    /// corresponds to the predicate that is always false (i.e., no encountered node
    /// will be ignored).</param>
    /// <param name="new_subgraph_visitor">Visitor for the root node of each
    /// new subgraph: the roots (see the roots parameter)
    /// are explored in order; if a root node has not been already reached
    /// by the DFS traversal of the previous roots, <c>new_subgraph_visitor</c>
    /// will be called on it, and next the subgraph rooted in it will be DFS
    /// traversed.</param>
    /// <param name="begin_visitor">Node visitor to be called when a node is reached
    /// for the first time by the DFS traversal. <c>null</c> corresponds to no
    /// visitor.</param>
    /// <param name="end_visitor">Node visitor to be called when the exploration of
    /// a node has finished. <c>null</c> corresponds to no visitor.</param>
    public static void SearchDepthFirst (
      IEnumerable roots, 
      IGraphNavigator navigator,
      DNodePredicate avoid,
      DNodeVisitor new_subgraph_visitor,
      DNodeVisitor begin_visitor,
      DNodeVisitor end_visitor
      )
    {
      // set of already seen nodes
      IMutableSet seen_nodes = new HashSet();
      // DFS Stack: holds the currently explored path; simulates the call
      // stack of a recursive implementation of DFS.
      Stack/*<NodeInfo>*/ stack = new Stack();

      foreach(object root in roots)
      {
        if( ((avoid != null) && avoid(root)) ||
          seen_nodes.Contains(root)) continue;

        call_visitor(new_subgraph_visitor, root);

        seen_nodes.Add(root);
        call_visitor(begin_visitor, root);
        stack.Push(new NodeInfo(root, navigator));
        while(stack.Count != 0)
        {
          NodeInfo info = (NodeInfo) stack.Peek();
          if(info.enext.MoveNext())
          {
            object next_node = info.enext.Current;
            // ignore nodes as dictated by "avoid"
            if((avoid != null) && avoid(next_node))
              continue;
						
            if(!seen_nodes.Contains(next_node))
            {
              // new and non-avoidable node!
              // mark it as seen,
              seen_nodes.Add(next_node);
              // call the begin visitor,
              call_visitor(begin_visitor, next_node);
              // and put the node on the DFS stack
              stack.Push(new NodeInfo(next_node, navigator));
            }
          }
          else 
          {
            // the visit of info.node has finished
            // apply end visitor
            call_visitor(end_visitor, info.node);
            // remove the top of the stack
            stack.Pop();
          }
        }
      }
    }


    /// <summary>
    /// Convenient <c>DFS</c> function.  Call the full <c>dfs</c> function
    /// with new_subgraph_visitor set to <c>null</c>.
    /// </summary>
    public static void dfs (
      IEnumerable roots, 
      IGraphNavigator navigator,
      DNodePredicate avoid,
      DNodeVisitor begin_visitor,
      DNodeVisitor end_visitor)
    {
      SearchDepthFirst(roots, navigator, avoid, null, begin_visitor, end_visitor);
    }

    private static void call_visitor( DNodeVisitor visitor, object node)
    {
      if(visitor != null)
        visitor(node);
    }

    // private class used internally by the DFS method.
    private struct NodeInfo
    {
      public object node;
      public IEnumerator enext ;
      public NodeInfo(object node, IGraphNavigator navigator)
      {
        this.node  = node;
        this.enext = navigator.NextNodes(node).GetEnumerator();
      }
    }



    /// <summary>
    /// Does a breadth first traversal of the given graph
    /// </summary>
    /// <param name="roots">The roots of the traversal.</param>
    /// <param name="avoid">If not null, is a predicate to avoid certain nodes</param>
    /// <param name="visitRoot">If not null, called for each root that is not avoided.</param>
    /// <param name="visitEdge">Called for each edges in the bf traversal, i.e., only for edges going to unvisited nodes.</param>
    public static void bfs(IEnumerable roots, IGraphNavigator navigator,
      DNodePredicate avoid,
      DNodeVisitor visitRoot,
      DEdgeVisitor visitEdge)
    {
      Queue queue = new Queue();
      IMutableSet seen = new HashSet();

      // initialize queue with roots
      foreach(object o in roots) 
      {
        if (avoid==null || !avoid(o)) 
        {
          queue.Enqueue(o);
          seen.Add(o);
          if (visitRoot != null) visitRoot(o);
        }
      }

      while(queue.Count > 0) 
      {
        object node = queue.Dequeue();
        foreach(object succ in navigator.NextNodes(node)) 
        {
          if ((avoid == null || !avoid(succ)) && !seen.Contains(succ)) 
          {
            seen.Add(succ);
            queue.Enqueue(succ);
            visitEdge(node, succ);
          }
        }
      }
    }



    interface IVisit {
      void Visit(object node);
    }

    private class TargetInfo {
      public object Target;
      public bool Found;

      public TargetInfo(object target) { 
        this.Target = target;
        this.Found = false;
      }

      public void Visit(object node) {
        if (this.Target == node) {
          this.Found = true;
        }
      }

    }
    public static bool IsReachable(IGraphNavigator navigator, object from, object to) {
      TargetInfo targetInfo = new TargetInfo(to);
      dfs(new object[1]{from}, navigator, null, new DNodeVisitor(targetInfo.Visit), null);
      return targetInfo.Found;
    }

    private class BFSpanningBuilder : MapBasedNavigator
    {
      public BFSpanningBuilder () 
        : base (new Relation(), new Relation())
      {
      }

      public void VisitEdge(object from, object to) 
      {
        this.n2next.Add(from, to);
        this.n2prev.Add(to, from);
      }
    }


    public static IGraphNavigator BFSpanningTree(IEnumerable roots, IGraphNavigator navigator, 
      DNodePredicate avoid) 
    {
      BFSpanningBuilder bfb = new BFSpanningBuilder();

      bfs(roots, navigator, avoid, null, new DEdgeVisitor(bfb.VisitEdge));

      return bfb;
    }


    private class GraphDepthComputer {
      private int currentDepth = 0;
      private int maxDepth = 0;
      private DDepthAssigner da;

      public GraphDepthComputer(DDepthAssigner da) {
        this.da = da;
      }

      public void BeginVisitor(object node) {
        currentDepth++;
        if (da != null) da(node, currentDepth);
        if (currentDepth > maxDepth) {
          maxDepth = currentDepth;
        }
      }

      public void EndVisitor(object node) {
        currentDepth--;
      }

      public int MaxDepth { get { return this.maxDepth; } }
    }

    public delegate void DDepthAssigner(object node, int depth);

    public static int GraphDepth (IGraphNavigator graph, object startNode, DDepthAssigner da) {
      GraphDepthComputer gdc = new GraphDepthComputer(da);

      dfs(new object[]{startNode}, graph, null, new DNodeVisitor(gdc.BeginVisitor), new DNodeVisitor(gdc.EndVisitor));
      return gdc.MaxDepth;
    }

  }

}
