
function PLUS(int, int, int) returns (int);
function Rep(int, int) returns (int);

//PLUS(a,b,z)
// ERROR

axiom(forall a:int, b:int, z:int :: Rep(a,b) == Rep(a,0));
axiom(forall n:int, x:int :: {Rep(n,x)} (exists k:int :: Rep(n,x) == x));
// END ERROR


procedure  main ( ) 
{ 
assert (PLUS(0, 4, 55)!=0);
}

