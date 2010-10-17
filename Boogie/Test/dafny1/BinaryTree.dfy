class IntSet {
  ghost var Contents: set<int>;
  ghost var Repr: set<object>;

  var root: Node;

  function Valid(): bool
    reads this, Repr;
  {
    this in Repr &&
    (root == null ==> Contents == {}) &&
    (root != null ==>
       root in Repr && root.Repr <= Repr && this !in root.Repr &&
       root.Valid() &&
       Contents == root.Contents)
  }

  method Init()
    modifies this;
    ensures Valid() && fresh(Repr - {this});
    ensures Contents == {};
  {
    root := null;
    Repr := {this};
    Contents := {};
  }

  method Find(x: int) returns (present: bool)
    requires Valid();
    ensures present <==> x in Contents;
  {
    if (root == null) {
      present := false;
    } else {
      call present := root.Find(x);
    }
  }

  method Insert(x: int)
    requires Valid();
    modifies Repr;
    ensures Valid() && fresh(Repr - old(Repr));
    ensures Contents == old(Contents) + {x};
  { 
    call t := InsertHelper(x, root);
    root := t;
    Contents := root.Contents;
    Repr := root.Repr + {this};
  }

  static method InsertHelper(x: int, n: Node) returns (m: Node)
    requires n == null || n.Valid();
    modifies n.Repr;
    ensures m != null && m.Valid();
    ensures n == null ==> fresh(m.Repr) && m.Contents == {x};
    ensures n != null ==> m == n && n.Contents == old(n.Contents) + {x};
    ensures n != null ==> fresh(n.Repr - old(n.Repr));
    decreases if n == null then {} else n.Repr;
  {
    if (n == null) {
      m := new Node;
      call m.Init(x);
    } else if (x == n.data) {
      m := n;
    } else {
      if (x < n.data) {
        assert n.right == null || n.right.Valid();
        call t := InsertHelper(x, n.left);
        n.left := t;
        n.Repr := n.Repr + n.left.Repr;
      } else {
        assert n.left == null || n.left.Valid();
        call t := InsertHelper(x, n.right);
        n.right := t;
        n.Repr := n.Repr + n.right.Repr;
      }
      n.Contents := n.Contents + {x};
      m := n;
    }
  }

  method Remove(x: int)
    requires Valid();
    modifies Repr;
    ensures Valid() && fresh(Repr - old(Repr));
    ensures Contents == old(Contents) - {x};
  {
    if (root != null) {
      call newRoot := root.Remove(x);
      root := newRoot;
      if (root == null) {
        Contents := {};
        Repr := {this};
      } else {
        Contents := root.Contents;
        Repr := root.Repr + {this};
      }
    }
  }
}

class Node {
  ghost var Contents: set<int>;
  ghost var Repr: set<object>;

  var data: int;
  var left: Node;
  var right: Node;

  function Valid(): bool
    reads this, Repr;
  {
    this in Repr &&
    null !in Repr &&
    (left != null ==>
      left in Repr &&
      left.Repr <= Repr && this !in left.Repr &&
      left.Valid() &&
      (forall y :: y in left.Contents ==> y < data)) &&
    (right != null ==>
      right in Repr &&
      right.Repr <= Repr && this !in right.Repr &&
      right.Valid() &&
      (forall y :: y in right.Contents ==> data < y)) &&
    (left == null && right == null ==>
      Contents == {data}) &&
    (left != null && right == null ==>
      Contents == left.Contents + {data}) &&
    (left == null && right != null ==>
      Contents == {data} + right.Contents) &&
    (left != null && right != null ==>
      left.Repr !! right.Repr &&
      Contents == left.Contents + {data} + right.Contents)
  }

  method Init(x: int)
    modifies this;
    ensures Valid() && fresh(Repr - {this});
    ensures Contents == {x};
  {
    data := x;
    left := null;
    right := null;
    Contents := {x};
    Repr := {this};
  }

  method Find(x: int) returns (present: bool)
    requires Valid();
    ensures present <==> x in Contents;
    decreases Repr;
  {
    if (x == data) {
      present := true;
    } else if (left != null && x < data) {
      call present := left.Find(x);
    } else if (right != null && data < x) {
      call present := right.Find(x);
    } else {
      present := false;
    }
  }

  method Remove(x: int) returns (node: Node)
    requires Valid();
    modifies Repr;
    ensures fresh(Repr - old(Repr));
    ensures node != null ==> node.Valid();
    ensures node == null ==> old(Contents) <= {x};
    ensures node != null ==> node.Repr <= Repr && node.Contents == old(Contents) - {x};
    decreases Repr;
  {
    node := this;
    if (left != null && x < data) {
      call t := left.Remove(x);
      left := t;
      Contents := Contents - {x};
      if (left != null) { Repr := Repr + left.Repr; }
    } else if (right != null && data < x) {
      call t := right.Remove(x);
      right := t;
      Contents := Contents - {x};
      if (right != null) { Repr := Repr + right.Repr; }
    } else if (x == data) {
      if (left == null && right == null) {
        node := null;
      } else if (left == null) {
        node := right;
      } else if (right == null) {
        node := left;
      } else {
        // rotate
        call min, r := right.RemoveMin();
        data := min;  right := r;
        Contents := Contents - {x};
        if (right != null) { Repr := Repr + right.Repr; }
      }
    }
  }

  method RemoveMin() returns (min: int, node: Node)
    requires Valid();
    modifies Repr;
    ensures fresh(Repr - old(Repr));
    ensures node != null ==> node.Valid();
    ensures node == null ==> old(Contents) == {min};
    ensures node != null ==> node.Repr <= Repr && node.Contents == old(Contents) - {min};
    ensures min in old(Contents) && (forall x :: x in old(Contents) ==> min <= x);
    decreases Repr;
  {
    if (left == null) {
      min := data;
      node := right;
    } else {
      call min, t := left.RemoveMin();
      left := t;
      node := this;
      Contents := Contents - {min};
      if (left != null) { Repr := Repr + left.Repr; }
    }
  }
}

class Main {
  method Client0(x: int)
  {
    var s := new IntSet;
    call s.Init();

    call s.Insert(12);
    call s.Insert(24);
    call present := s.Find(x);
    assert present <==> x == 12 || x == 24;
  }

  method Client1(s: IntSet, x: int)
    requires s != null && s.Valid();
    modifies s.Repr;
  {
    call s.Insert(x);
    call s.Insert(24);
    assert old(s.Contents) - {x,24} == s.Contents - {x,24};
  }
}
