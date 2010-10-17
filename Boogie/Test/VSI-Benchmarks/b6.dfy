
class Collection<T> {
  ghost var footprint:set<object>;
  var elements:seq<T>;
  
  function Valid():bool
    reads this, footprint;
  {
    this in footprint
  }
  
  method GetCount() returns (c:int)
    requires Valid();
    ensures 0<=c;
  {
    c:=|elements|;
  }
  
  method Init()
    modifies this;
    ensures Valid() && fresh(footprint -{this});
  {
    elements := [];
    footprint := {this}; 
  }
  
  method GetItem(i:int ) returns (x:T)
    requires Valid();
    requires 0<=i && i<|elements|;
    ensures elements[i] ==x;
  {
    x:=elements[i];
  }
  
  method Add(x:T )
    requires Valid();
    modifies footprint;
    ensures Valid() && fresh(footprint - old(footprint));
    ensures elements == old(elements) + [x];
  {
    elements:= elements + [x];
  }
  
  method GetIterator() returns (iter:Iterator<T>)
    requires Valid();
    ensures iter != null && iter.Valid();
    ensures fresh(iter.footprint) && iter.pos == -1;
    ensures iter.c == this;
  {
      iter:= new Iterator<T>;
      call iter.Init(this);
  }
  
}

class Iterator<T> {
 
  var c:Collection<T>;
  var pos:int;
  
  ghost var footprint:set<object>;
  
  function Valid():bool
    reads this, footprint;
  {
    this in footprint && c != null && -1 <= pos && null !in footprint
  }
  
  method Init(coll:Collection<T>)
    requires coll != null;
    modifies this;
    ensures Valid() && fresh(footprint - {this}) && pos == -1;
    ensures c == coll;
  {
    c := coll;
    pos := -1;
    footprint := {this}; 
  }
  
  method MoveNext() returns (b:bool)
    requires Valid();
    modifies footprint;
    ensures fresh(footprint - old(footprint)) &&  Valid()  && pos == old(pos) + 1;
    ensures b == HasCurrent() && c == old(c);
  {
    pos := pos+1;
    b := pos < |c.elements|;
  }
  
  function HasCurrent():bool //???
    requires Valid();
    reads this, c;
  {
    0 <= pos && pos < |c.elements|
  }
 
  method GetCurrent() returns (x:T)
    requires Valid() && HasCurrent();
    ensures c.elements[pos] == x;
  {
    x := c.elements[pos];
  }
} 

class Client
{

  method Main()
  {
    var c := new Collection<int>;
    call c.Init();
    call c.Add(33);
    call c.Add(45);
    call c.Add(78);
    
    var s := [];
    
    call iter := c.GetIterator();
    call b := iter.MoveNext();
    
    while (b)
      invariant iter.Valid() && b == iter.HasCurrent() && fresh(iter.footprint);
      invariant c.Valid() && fresh(c.footprint) && iter.footprint !! c.footprint; //disjoint footprints
      invariant 0 <= iter.pos && iter.pos <=|c.elements| && s == c.elements[..iter.pos] ;
      invariant iter.c == c;
      decreases |c.elements| - iter.pos;
    {   
      call x := iter.GetCurrent();
      s := s + [x];
      call b := iter.MoveNext();
    }
    
    assert s == c.elements; //verifies that the iterator returns the correct things
    call c.Add(100);
  }
  
}
