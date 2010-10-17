


var p : <a>[int]a;

procedure P() returns () modifies p; {
  p[13] := 5;
  p[17] := true;
  p[13] := false;
  p[17] := 8;

  assert p[13] == 5 && !p[13] && p[17] == 8 && p[17];
  assert p == p[28 := p];      // error
}

var q : <a, b>[int][a]b;

procedure Q() returns () modifies q; {
  q[17] := q[17][true := 13];
  q[17] := q[17][true := false];
  q[16] := q[17][true := 14];

  assert q[17][true] == 13 && !q[17][true];
  assert q[17][true] == 14;    // error
}

procedure R() returns () modifies p; {
  p[7] := 28;
  p[5] := p[7];

  assert p[7] == 28;
  assert p[6] == 28;       // error
}