
type Set = <a> [a] bool;
type Field a;
type Heap = <b> [ref, Field b] b;


const emptySet : Set;
axiom (forall<t> x:t :: !emptySet[x]);

procedure P() returns () {
  var x : Set, f : Field Set, g : Field int, heap : Heap, o : ref;

  x := emptySet;
  heap[o, f] := x;
  heap[o, g] := 13;
  assert heap[o, f] == emptySet && heap[o, g] == 13;

  heap[o, f] := heap[o, f][17 := true];
  heap[o, f] := heap[o, f][g := true];

  assert (forall<t> y:t :: heap[o, f][y] == (y == 17 || y == g));
  assert (forall<t> y:t :: heap[o, f][y] == (y == 16 || y == g));    // should not hold

}

type ref;

