
function a() returns(bv32);
axiom a() == a();

axiom 0bv5 != 1bv5;


// -------------------------
type $x;
function g() returns($x);
type Field x;
var $gmem : <x>[ref, Field x] x;
const unique f : Field $x;

procedure qq()
  modifies $gmem;
{ 
   $gmem[null, f] := g();
}


type ref;
const null : ref;
