// Example from the Boogie 2 language report


type Wicket;


const unique a: Wicket extends complete;
const unique b: Wicket;
const unique c: Wicket extends a, b complete;
const unique d: Wicket extends c;
const unique e: Wicket;

procedure P() returns () {

  assert !(exists x:Wicket :: a <: x && a != x);
  assert (forall x:Wicket :: x <: a ==> x == a || x <: c);

  assert c <: b && !(exists x:Wicket :: c <: x && x <: b && x != c && x != b);

  assert !(b <: a) && !(b <: c);

  assert c <: a && c <: b && d <: c;
  assert (forall x:Wicket :: c <: x ==> c==x || a <: x || b <: x);
  assert (forall x:Wicket :: x <: c ==> c==x || x <: d);

  assert d <: c;
  assert !(a <: d) && !(b <: d) && !(c <: d);

  assert false;           // unprovable
}

procedure Q() returns () {
  
  assert (forall x:Wicket :: x <: b && x != b ==> x <: c);   // unprovable

  assert !(exists x:Wicket :: b <: x && b != x);             // unprovable

}