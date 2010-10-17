
procedure P() returns () {

 assume (forall<t> m : [t]bool ::                // uses "infinitely many" map types
         (forall x : t ::    m[x] == false));

}


procedure Q() returns () {
 var h : [int] bool;

 assume (forall<t> m : [t]bool, x : t ::    m[x] == false);
 assert !h[42];
 assert false;                    // should really be provable
}



procedure R() returns () {
 var h : [int] bool;

 assume (forall<t> m : [t]bool, x : t ::    m[x] == false);
 assert !h[42];
 assert !h[42 := true][42];
 assert false;                    // wow
}
