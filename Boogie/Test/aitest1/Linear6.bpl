// Simple test file for checking the inference of linear constraints.

var x: int;
var y: int;
var z: int;

procedure p()
  modifies x;
{
A:
    x := 8;
    goto B, C;
B:
    x := 9;
    goto D;
C:
    x := 10;
    goto D;
D:
    return;
}
