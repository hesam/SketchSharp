
type Box;

function box<a>(a) returns (Box);
function unbox<a>(Box) returns (a);

axiom (forall<a> x:a :: unbox(box(x)) == x);

var b1: Box;
var b2: Box;
var b3: Box;

procedure P() returns ()
  modifies b1, b2, b3; {
  b1 := box(13);
  b2 := box(true);
  b3 := box(b1);

  assert unbox(b1) == 13 && unbox(b2) == true && unbox(unbox(b3)) == 13;
  assert unbox(b1) == true;    // error
}