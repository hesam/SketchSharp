const TEST: name;

procedure P()
{
var i: int;
var N: int;

start:
  assume N >= 0;
  i := 0;
  assert i <= N;
  goto LoopHead;

LoopHead:
  goto LoopBody, AfterLoop;

LoopBody:
  assume i < N;
  i := i + 1;
  goto LoopHead;

AfterLoop:
  assume !(i < N);
  assert i == N;
  return;
}

type name;