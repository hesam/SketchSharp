// *********************************************
// *                                          *
// *   Boogie 2 prelude for MSIL translator   *
// *                                          *
// ********************************************


//------------ New types

type TName;
type real;
type Elements alpha;
type struct;

const $ZeroStruct: struct;

//------------ Encode the heap

type ref;
const null: ref;

type Field alpha;

type HeapType = <beta>[ref, Field beta]beta;
var $Heap : HeapType where IsHeap($Heap);

type ActivityType;
var $ActivityIndicator : ActivityType;


// IsHeap(h) holds if h is a properly formed heap
function IsHeap(h: HeapType) returns (bool);
// IsPreHeap(h) holds if the heap is properly formed, except
// that object-invariant properties (like "o.inv ==> Inv(o)")
// may not hold.
function IsPreHeap(h: HeapType) returns (bool);
axiom (forall h: HeapType :: { IsPreHeap(h) }  IsHeap(h) ==> IsPreHeap(h));


// records whether a pointer refers to allocated data
const unique $allocated : Field bool;

// the elements from dereferencing an array pointer
const unique $elementsBool : Field (Elements bool);
const unique $elementsInt : Field (Elements int);
const unique $elementsRef : Field (Elements ref);
const unique $elementsReal : Field (Elements real);
const unique $elementsStruct : Field (Elements struct);
axiom DeclType($elementsBool) == System.Array;
axiom DeclType($elementsInt) == System.Array;
axiom DeclType($elementsRef) == System.Array;
axiom DeclType($elementsReal) == System.Array;
axiom DeclType($elementsStruct) == System.Array;

#if TrivialObjectModel
function $Inv(h: HeapType, o: ref, frame: TName) returns (bool);
function $InvExclusion(ref) returns (bool);

function $KnownClass(cl: TName) returns (bool);

// System.Object class invariant
axiom (forall $oi: ref, $h: HeapType :: { $Inv($h, $oi, System.Object) } $Inv($h, $oi, System.Object));

// array types class invariants
axiom (forall $oi: ref, $h: HeapType, T: TName :: { $Inv($h, $oi, T), T <: System.Array } T <: System.Array ==> $Inv($h, $oi, T));

#elsif ExperimentalObjectModel
// the name of the most derived class for which an object invariant holds
const unique $inv : Field TName;

// array, indexed by class names, that keeps a boolean showing the invariant holds for that class on the object given
const unique $validfor: Field [TName]bool;

axiom (forall h: HeapType, o: ref, T: TName :: {h[o, $validfor][T]} 
    IsHeap(h) && o != null &&  $typeof(o) <: T && $BaseClass(T) <: h[o, $inv] ==> !h[o, $validfor][T]);
axiom (forall h: HeapType, o: ref :: {h[o,$inv]} 
    IsHeap(h) && o != null && h[o,$validfor][$typeof(o)] ==> h[o,$inv] == $typeof(o));

#else
// the name of the most derived class for which an object invariant holds
const unique $inv : Field TName;

// the name of (the supertype of) the class for which the object invariant is allowed to be broken
const unique $localinv: Field TName;

#endif

// dummy field that is havoced at unpacks so that it can be used to deduce state changes  
type exposeVersionType;
const unique $exposeVersion : Field exposeVersionType;

// declaration type of exposeVersion is System.Object
axiom DeclType($exposeVersion) == System.Object;

#if !TrivialObjectModel
// the $sharingMode field indicates the object's sharing mode, which is either $SharingMode_Unshared or $SharingMode_LockProtected
type SharingMode;
const unique $sharingMode : Field SharingMode;
const unique $SharingMode_Unshared : SharingMode;
const unique $SharingMode_LockProtected : SharingMode;

// a reference to the object ($ownerRef, $ownerFrame)
const unique $ownerRef : Field ref;
const unique $ownerFrame : Field TName;
const unique $PeerGroupPlaceholder : TName;  // used as a type name
#endif

// a map from class names to their representative "references" used to obtain values of static fields
function ClassRepr(class: TName) returns (ref);
// this map is injective
function ClassReprInv(ref) returns (TName);
axiom (forall c: TName :: {ClassRepr(c)} ClassReprInv(ClassRepr(c)) == c);
axiom (forall T: TName :: !($typeof(ClassRepr(T)) <: System.Object));
axiom (forall T: TName :: ClassRepr(T) != null);
#if !TrivialObjectModel
axiom (forall T: TName, h: HeapType :: {h[ClassRepr(T),$ownerFrame]} IsHeap(h) ==> h[ClassRepr(T),$ownerFrame] == $PeerGroupPlaceholder);
#endif

// The following function lets the translation utter an expression, with the hope that it
// will cause good things to be triggered.
function $Utter(ref) returns (bool);

//------------ Fields
// fields are classified into whether or not the field is static (i.e., it is a field of a ClassRepr "ref") 
// and whether or not it is directly modifiable by the user

// indicates a field has to be part of the frame condition
function IncludeInMainFrameCondition<alpha>(f: Field alpha) returns (bool);
axiom IncludeInMainFrameCondition($allocated);
axiom IncludeInMainFrameCondition($elementsBool) &&
      IncludeInMainFrameCondition($elementsInt) &&
      IncludeInMainFrameCondition($elementsRef) &&
      IncludeInMainFrameCondition($elementsReal) &&
      IncludeInMainFrameCondition($elementsStruct);
#if !TrivialObjectModel
axiom !(IncludeInMainFrameCondition($inv));
#if ExperimentalObjectModel
axiom !(IncludeInMainFrameCondition($validfor));
#else
axiom !(IncludeInMainFrameCondition($localinv));
#endif
axiom IncludeInMainFrameCondition($ownerRef);
axiom IncludeInMainFrameCondition($ownerFrame);
#endif
axiom IncludeInMainFrameCondition($exposeVersion);
#if !TrivialObjectModel
axiom !(IncludeInMainFrameCondition($FirstConsistentOwner));
#endif

// indicates a field is static
function IsStaticField<alpha>(f: Field alpha) returns (bool);
axiom !IsStaticField($allocated);
axiom !IsStaticField($elementsBool) &&
      !IsStaticField($elementsInt) &&
      !IsStaticField($elementsRef) &&
      !IsStaticField($elementsReal) &&
      !IsStaticField($elementsStruct);
#if !TrivialObjectModel
axiom !IsStaticField($inv);
#if ExperimentalObjectModel
axiom !IsStaticField($validfor);
#else
axiom !IsStaticField($localinv);
#endif
#endif
axiom !IsStaticField($exposeVersion);

// indicates if a is included in modifies o.* and o.**
function $IncludedInModifiesStar<alpha>(f: Field alpha) returns (bool);
#if !TrivialObjectModel
axiom !$IncludedInModifiesStar($ownerRef);
axiom !$IncludedInModifiesStar($ownerFrame);
#endif
// $inv and $localinv are not included either, but we don't need to say that in an axiom
// the same for $validfor
axiom $IncludedInModifiesStar($exposeVersion);
axiom $IncludedInModifiesStar($elementsBool) &&
      $IncludedInModifiesStar($elementsInt) &&
      $IncludedInModifiesStar($elementsRef) &&
      $IncludedInModifiesStar($elementsReal) &&
      $IncludedInModifiesStar($elementsStruct);


//------------ Array elements

function ArrayGet<alpha>(Elements alpha, int) returns (alpha);
function ArraySet<alpha>(Elements alpha, int, alpha) returns (Elements alpha);

axiom (forall<alpha> A: Elements alpha, i: int, x: alpha :: ArrayGet(ArraySet(A, i, x), i) == x);
axiom (forall<alpha> A: Elements alpha, i: int, j: int, x: alpha :: i != j  ==> ArrayGet(ArraySet(A, i, x), j) == ArrayGet(A, j)); 

// the indices of multi-dimensional arrays are built up one dimension at a time
function ArrayIndex(arr: ref, dim: int, indexAtDim: int, remainingIndexContribution: int) returns (int);
// the expressions built up are injective in the indices
function ArrayIndexInvX(arrayIndex: int) returns (indexAtDim: int);
function ArrayIndexInvY(arrayIndex: int) returns (remainingIndexContribution: int);
axiom (forall a:ref, d:int, x: int, y: int ::  {ArrayIndex(a,d,x,y)}  ArrayIndexInvX(ArrayIndex(a,d,x,y)) == x);
axiom (forall a:ref, d:int, x: int, y: int ::  {ArrayIndex(a,d,x,y)}  ArrayIndexInvY(ArrayIndex(a,d,x,y)) == y);

axiom (forall a:ref, i:int, heap:HeapType ::
   { ArrayGet(heap[a, $elementsInt], i) }
   IsHeap(heap) ==>  InRange(ArrayGet(heap[a, $elementsInt], i), $ElementType($typeof(a))));
axiom (forall a:ref, i:int, heap:HeapType ::
    { $typeof(ArrayGet(heap[a, $elementsRef], i)) }
    IsHeap(heap) && ArrayGet(heap[a, $elementsRef], i) != null  ==>
    $typeof(ArrayGet(heap[a, $elementsRef], i)) <: $ElementType($typeof(a)));
axiom (forall a:ref, T:TName, i:int, r:int, heap:HeapType ::
    { $typeof(a) <: NonNullRefArray(T, r), ArrayGet(heap[a, $elementsRef], i) }
    IsHeap(heap) && $typeof(a) <: NonNullRefArray(T,r)  ==>  ArrayGet(heap[a, $elementsRef], i) != null);

//------------ Array properties: rank, length, dimensions, upper and lower bounds

function $Rank (ref) returns (int); 
axiom (forall a:ref :: 1 <= $Rank(a));
axiom (forall a:ref, T:TName, r:int :: {$typeof(a) <: RefArray(T,r)} a != null && $typeof(a) <: RefArray(T,r)  ==> $Rank(a) == r);
axiom (forall a:ref, T:TName, r:int :: {$typeof(a) <: NonNullRefArray(T,r)} a != null && $typeof(a) <: NonNullRefArray(T,r)  ==> $Rank(a) == r);
axiom (forall a:ref, T:TName, r:int :: {$typeof(a) <: ValueArray(T,r)} a != null && $typeof(a) <: ValueArray(T,r)  ==> $Rank(a) == r);
axiom (forall a:ref, T:TName, r:int :: {$typeof(a) <: IntArray(T,r)} a != null && $typeof(a) <: IntArray(T,r)  ==> $Rank(a) == r);

function $Length (ref) returns (int);
axiom (forall a:ref :: {$Length(a)} 0 <= $Length(a) && $Length(a) <= 2147483647);

function $DimLength (ref, int) returns (int); // length per dimension up to rank
axiom (forall a:ref, i:int :: 0 <= $DimLength(a,i));
// The trigger used in the following axiom is restrictive, so that this disjunction is not
// produced too easily.  Is the trigger perhaps sometimes too restrictive?
axiom (forall a:ref :: { $DimLength(a,0) }  $Rank(a) == 1 ==> $DimLength(a,0) == $Length(a));

function $LBound (ref, int) returns (int); 
function $UBound (ref, int) returns (int);
// Right now we only model C# arrays:
axiom (forall a:ref, i:int :: {$LBound(a,i)} $LBound(a,i) == 0);
axiom (forall a:ref, i:int :: {$UBound(a,i)} $UBound(a,i) == $DimLength(a,i)-1);

// Different categories of arrays are different types

type ArrayCategory;
const unique $ArrayCategoryValue: ArrayCategory;
const unique $ArrayCategoryInt: ArrayCategory;
const unique $ArrayCategoryRef: ArrayCategory;
const unique $ArrayCategoryNonNullRef: ArrayCategory;

function $ArrayCategory(arrayType: TName) returns (arrayCategory: ArrayCategory);

axiom (forall T: TName, ET: TName, r: int :: { T <: ValueArray(ET, r) } T <: ValueArray(ET, r) ==> $ArrayCategory(T) == $ArrayCategoryValue);
axiom (forall T: TName, ET: TName, r: int :: { T <: IntArray(ET, r) } T <: IntArray(ET, r) ==> $ArrayCategory(T) == $ArrayCategoryInt);
axiom (forall T: TName, ET: TName, r: int :: { T <: RefArray(ET, r) } T <: RefArray(ET, r) ==> $ArrayCategory(T) == $ArrayCategoryRef);
axiom (forall T: TName, ET: TName, r: int :: { T <: NonNullRefArray(ET, r) } T <: NonNullRefArray(ET, r) ==> $ArrayCategory(T) == $ArrayCategoryNonNullRef);

//------------ Array types

const unique System.Array : TName;
axiom System.Array <: System.Object;

function $ElementType(TName) returns (TName);

function ValueArray (elementType:TName, rank:int) returns (TName); 
axiom (forall T:TName, r:int :: {ValueArray(T,r)} ValueArray(T,r) <: ValueArray(T,r) && ValueArray(T,r) <: System.Array);
function IntArray (elementType:TName, rank:int) returns (TName); 
axiom (forall T:TName, r:int :: {IntArray(T,r)} IntArray(T,r) <: IntArray(T,r) && IntArray(T,r) <: System.Array);

function RefArray (elementType:TName, rank:int) returns (TName); 
axiom (forall T:TName, r:int :: {RefArray(T,r)} RefArray(T,r) <: RefArray(T,r) && RefArray(T,r) <: System.Array);
function NonNullRefArray (elementType:TName, rank:int) returns (TName); 
axiom (forall T:TName, r:int :: {NonNullRefArray(T,r)} NonNullRefArray(T,r) <: NonNullRefArray(T,r) && NonNullRefArray(T,r) <: System.Array);
function NonNullRefArrayRaw(array: ref, elementType: TName, rank: int) returns (bool);
axiom (forall array: ref, elementType: TName, rank: int ::  { NonNullRefArrayRaw(array, elementType, rank) }
  NonNullRefArrayRaw(array, elementType, rank)
  ==>  $typeof(array) <: System.Array && $Rank(array) == rank && elementType <: $ElementType($typeof(array)));

// arrays of references are co-variant
axiom (forall T:TName, U:TName, r:int :: U <: T  ==>  RefArray(U,r) <: RefArray(T,r));
axiom (forall T:TName, U:TName, r:int :: U <: T  ==>  NonNullRefArray(U,r) <: NonNullRefArray(T,r));

axiom (forall A: TName, r: int :: $ElementType(ValueArray(A,r)) == A);
axiom (forall A: TName, r: int :: $ElementType(IntArray(A,r)) == A);
axiom (forall A: TName, r: int :: $ElementType(RefArray(A,r)) == A);
axiom (forall A: TName, r: int :: $ElementType(NonNullRefArray(A,r)) == A);

// subtypes of array types
axiom (forall A: TName, r: int, T: TName ::  {T <: RefArray(A,r)} T <: RefArray(A,r)  ==>  T != A && T == RefArray($ElementType(T),r) && $ElementType(T) <: A);
axiom (forall A: TName, r: int, T: TName ::  {T <: NonNullRefArray(A,r)} T <: NonNullRefArray(A,r)  ==>  T != A && T == NonNullRefArray($ElementType(T),r) && $ElementType(T) <: A);
axiom (forall A: TName, r: int, T: TName ::  {T <: ValueArray(A, r)} T <: ValueArray(A, r)  ==>  T == ValueArray(A, r));
axiom (forall A: TName, r: int, T: TName ::  {T <: IntArray(A, r)} T <: IntArray(A, r)  ==>  T == IntArray(A, r));

// supertypes of array types
axiom (forall A: TName, r: int, T: TName ::  {RefArray(A,r) <: T}  RefArray(A,r) <: T  ==>  System.Array <: T || (T == RefArray($ElementType(T),r) && A <: $ElementType(T)));
axiom (forall A: TName, r: int, T: TName ::  {NonNullRefArray(A,r) <: T}  NonNullRefArray(A,r) <: T  ==>  System.Array <: T || (T == NonNullRefArray($ElementType(T),r) && A <: $ElementType(T)));
axiom (forall A: TName, r: int, T: TName ::  {ValueArray(A, r) <: T}  ValueArray(A, r) <: T  ==>  System.Array <: T || T == ValueArray(A, r));
axiom (forall A: TName, r: int, T: TName ::  {IntArray(A, r) <: T}  IntArray(A, r) <: T  ==>  System.Array <: T || T == IntArray(A, r));

function $ArrayPtr (elementType:TName) returns (TName); 


//------------ Array and generic element ownership
function $ElementProxy(ref, int) returns (ref);
function $ElementProxyStruct(struct, int) returns (ref);


#if !TrivialObjectModel
axiom (forall a: ref, i: int, heap: HeapType :: { heap[ArrayGet(heap[a, $elementsRef], i),$ownerRef] } { heap[ArrayGet(heap[a, $elementsRef], i),$ownerFrame] } IsHeap(heap) && $typeof(a) <: System.Array ==> ArrayGet(heap[a, $elementsRef], i) == null || $IsImmutable($typeof(ArrayGet(heap[a, $elementsRef], i))) || (heap[ArrayGet(heap[a, $elementsRef], i),$ownerRef] == heap[$ElementProxy(a,-1),$ownerRef] && heap[ArrayGet(heap[a, $elementsRef], i),$ownerFrame] == heap[$ElementProxy(a,-1),$ownerFrame]));
#endif

axiom (forall a: ref, heap: HeapType :: { IsAllocated(heap,a) } IsHeap(heap) && IsAllocated(heap,a) && $typeof(a) <: System.Array ==> IsAllocated(heap, $ElementProxy(a,-1)));

axiom (forall o: ref, pos: int :: { $typeof($ElementProxy(o,pos)) } $typeof($ElementProxy(o,pos)) == System.Object);
axiom (forall o: struct, pos: int :: { $typeof($ElementProxyStruct(o,pos)) } $typeof($ElementProxyStruct(o,pos)) == System.Object);


//------------ Encode structs

function $StructGet<alpha>(struct, Field alpha) returns (alpha);

function $StructSet<alpha>(struct, Field alpha, alpha) returns (struct);


axiom (forall<alpha> s: struct, f: Field alpha, x: alpha ::  $StructGet($StructSet(s, f, x), f) == x);
		
axiom (forall<alpha,beta> s: struct, f: Field alpha, f': Field beta, x: alpha ::  f != f'  ==>  $StructGet($StructSet(s, f, x), f') == $StructGet(s, f')); 

function ZeroInit(s:struct, typ:TName) returns (bool);
// TODO: ZeroInit needs axiomatization that says the fields of s are 0 or null or ZeroInit, depending on their types

function ZeroInitStruct(TName): struct;
axiom (forall t: TName ::  { ZeroInitStruct(t) } ZeroInit(ZeroInitStruct(t), t));

//------------ Encode type information

function $typeof (ref) returns (TName);

function $BaseClass(sub: TName) returns (base: TName);
axiom (forall T: TName ::  { $BaseClass(T) }  T <: $BaseClass(T) &&  (!$IsValueType(T) && T != System.Object ==> T != $BaseClass(T)));

// Incomparable subtype axiom:
function AsDirectSubClass(sub: TName, base: TName) returns (sub': TName);
function OneClassDown(sub: TName, base: TName) returns (directSub: TName);
axiom (forall A: TName, B: TName, C: TName :: { C <: AsDirectSubClass(B,A) }  C <: AsDirectSubClass(B,A)  ==>  OneClassDown(C,A) == B);

// primitive types are unordered in the type ordering
function $IsValueType(TName) returns (bool);
axiom (forall T: TName :: $IsValueType(T)  ==>  (forall U: TName ::  T <: U  ==>  T == U) && (forall U: TName ::  U <: T  ==>  T == U));

const unique System.Boolean: TName;  // bool
axiom $IsValueType(System.Boolean);

// type constructor T[] 
//
const unique System.Object : TName;
axiom (forall T: TName ::  T <: System.Object ==> !$IsValueType(T));

// reflection
//
function $IsTokenForType (struct, TName) returns (bool);
function TypeObject (TName) returns (ref); // Corresponds with C# typeof(T)
const unique System.Type : TName;
axiom System.Type <: System.Object;
axiom (forall T:TName :: {TypeObject(T)} $IsNotNull(TypeObject(T), System.Type));
function TypeName(ref) returns (TName);  // the inverse of TypeObject, which is injective
axiom (forall T:TName :: {TypeObject(T)}  TypeName(TypeObject(T)) == T);

function $Is (ref, TName) returns (bool);
axiom (forall o:ref, T:TName :: {$Is(o, T)} $Is(o, T)  <==>  o == null || $typeof(o) <: T);

function $IsNotNull(ref, TName) returns (bool);
axiom (forall o:ref, T:TName :: {$IsNotNull(o, T)} $IsNotNull(o, T)  <==>  o != null && $Is(o,T));

// $As(o,T) is to be used only when T denotes a reference type (see also BoxTester).  It returns either o or null.
function $As (ref, TName) returns (ref);
axiom (forall o:ref, T:TName :: $Is(o, T)  ==>  $As(o, T) == o);
axiom (forall o:ref, T:TName :: ! $Is(o, T)  ==>  $As(o, T) == null);

// Arrays are always valid (but may be committed)
#if ExperimentalObjectModel
axiom (forall o: ref :: {$typeof(o) <: System.Array} o != null && $typeof(o) <: System.Array  ==> 
         (forall h: HeapType :: {h[o,$inv]} {h[o,$validfor]} IsHeap(h) ==>  
            (forall T: TName :: {h[o,$validfor][T]} h[o,$validfor][T]) && 
            h[o,$inv] == $typeof(o)));
#elsif !TrivialObjectModel
axiom (forall h: HeapType, o: ref :: {$typeof(o) <: System.Array, h[o,$inv]} IsHeap(h) && o != null && $typeof(o) <: System.Array  ==>  h[o,$inv] == $typeof(o) && h[o,$localinv] == $typeof(o));
#endif

//---------- Types and allocation of reachable things

function IsAllocated<alpha>(h: HeapType, o: alpha) returns (bool);

// everything in the range of a proper heap is allocated whenever the domain is
axiom (forall<alpha> h: HeapType, o: ref, f: Field alpha :: {IsAllocated(h, h[o,f])} IsHeap(h) && h[o, $allocated]  ==>  IsAllocated(h, h[o,f]));
axiom (forall h: HeapType, o: ref, f: Field ref :: {h[h[o,f], $allocated]} IsHeap(h) && h[o, $allocated]  ==>  h[h[o,f], $allocated]);

axiom (forall<alpha> h: HeapType, s: struct, f: Field alpha :: {IsAllocated(h, $StructGet(s,f))} IsAllocated(h,s)  ==>  IsAllocated(h, $StructGet(s,f)));
axiom (forall<alpha> h: HeapType, e: Elements alpha, i: int:: {IsAllocated(h, ArrayGet(e,i))} IsAllocated(h,e)  ==>  IsAllocated(h, ArrayGet(e,i)));

axiom (forall h: HeapType, o: ref :: {h[o, $allocated]}  IsAllocated(h,o)  ==>  h[o, $allocated]);

axiom (forall h: HeapType, c:TName :: {h[ClassRepr(c), $allocated]} IsHeap(h)  ==>  h[ClassRepr(c), $allocated]);

const $BeingConstructed: ref;
const unique $NonNullFieldsAreInitialized: Field bool;
const $PurityAxiomsCanBeAssumed: bool;
axiom DeclType($NonNullFieldsAreInitialized) == System.Object;

// types of fields
function DeclType<alpha>(field: Field alpha) returns (class: TName);  // for "class C { T f; ...", DeclType(f) == C
function AsNonNullRefField(field: Field ref, T: TName) returns (f: Field ref);  // for "class C { T! f; ...", AsNonNullRefField(f,T) == f
function AsRefField(field: Field ref, T: TName) returns (f: Field ref);  // for "class C { T f; ...", AsRefField(f,T) == f
// for integral types T
function AsRangeField(field: Field int, T: TName) returns (f: Field int);  // for "class C { T f; ...", AsRangeField(f,T) == f

axiom (forall f: Field ref, T: TName :: {AsNonNullRefField(f,T)}  AsNonNullRefField(f,T)==f  ==>  AsRefField(f,T)==f);

// fields in the heap are well typed
axiom (forall h: HeapType, o: ref, f: Field ref, T: TName :: {h[o,AsRefField(f,T)]}  IsHeap(h)  ==>  $Is(h[o,AsRefField(f,T)], T));
axiom (forall h: HeapType, o: ref, f: Field ref, T: TName :: {h[o,AsNonNullRefField(f,T)]}  IsHeap(h) && o != null && (o != $BeingConstructed || h[$BeingConstructed, $NonNullFieldsAreInitialized] == true) ==>  h[o,AsNonNullRefField(f,T)] != null);
axiom (forall h: HeapType, o: ref, f: Field int, T: TName :: {h[o,AsRangeField(f,T)]}  IsHeap(h)  ==>  InRange(h[o,AsRangeField(f,T)], T));

// abstract classes, interfaces, ...
function $IsMemberlessType(TName) returns (bool);
axiom (forall o: ref :: { $IsMemberlessType($typeof(o)) }  !$IsMemberlessType($typeof(o)));
function $AsInterface(TName) returns (TName);

axiom (forall J: TName :: { System.Object <: $AsInterface(J) }  $AsInterface(J) == J  ==>  !(System.Object <: J));

// this axiom relates a boxed struct to any interfaces that the struct implements
// otherwise, all that is known is that a boxed struct is of type System.Object which isn't strong enough
axiom (forall<T> $J: TName, s: T, b: ref :: { UnboxedType(Box(s,b)) <: $AsInterface($J) } $AsInterface($J) == $J && Box(s,b)==b && UnboxedType(Box(s,b)) <: $AsInterface($J) ==> $typeof(b) <: $J);

function $HeapSucc(oldHeap: HeapType, newHeap: HeapType) returns (bool);

//------------ Immutable types

function $IsImmutable(T:TName) returns (bool);

// We say here that System.Object is mutable, but only using the $IsImmutable predicate.  The functions
// $AsImmutable and $AsMutable below are used to say that all subtypes below fixpoints of these functions
// are also fixpoints.
axiom !$IsImmutable(System.Object);

function $AsImmutable(T:TName) returns (theType: TName);
function $AsMutable(T:TName) returns (theType: TName);

axiom (forall T: TName, U:TName :: {U <: $AsImmutable(T)} U <: $AsImmutable(T) ==>  $IsImmutable(U) && $AsImmutable(U) == U);
axiom (forall T: TName, U:TName :: {U <: $AsMutable(T)} U <: $AsMutable(T) ==>  !$IsImmutable(U) && $AsMutable(U) == U);

function AsOwner(string: ref, owner: ref) returns (theString: ref);

#if ExperimentalObjectModel
axiom (forall o: ref , T:TName :: {$typeof(o) <: $AsImmutable(T)}
    o != null && o != $BeingConstructed  && $typeof(o) <: $AsImmutable(T)
    ==> 
    (forall h: HeapType :: {IsHeap(h)}
        IsHeap(h)
        ==>
        (forall S:TName :: h[o,$validfor][S]) &&
        h[o, $inv] == $typeof(o) &&
        h[o, $ownerFrame] == $PeerGroupPlaceholder &&
        AsOwner(o, h[o, $ownerRef]) == o &&
        (forall t: ref ::  {AsOwner(o, h[t, $ownerRef])}
            AsOwner(o, h[t, $ownerRef]) == o
            ==>
            t == o  ||  h[t, $ownerFrame] != $PeerGroupPlaceholder)));
#elsif !TrivialObjectModel
axiom (forall o: ref , T:TName :: {$typeof(o) <: $AsImmutable(T)}
    o != null && o != $BeingConstructed  && $typeof(o) <: $AsImmutable(T)
    ==> 
    (forall h: HeapType :: {IsHeap(h)}
        IsHeap(h)
        ==>
        h[o, $inv] == $typeof(o) && h[o, $localinv] == $typeof(o) &&
        h[o, $ownerFrame] == $PeerGroupPlaceholder &&
        AsOwner(o, h[o, $ownerRef]) == o &&
        (forall t: ref ::  {AsOwner(o, h[t, $ownerRef])}
            AsOwner(o, h[t, $ownerRef]) == o
            ==>
            t == o  ||  h[t, $ownerFrame] != $PeerGroupPlaceholder)));
#endif

//------------ Encode methodology

const unique System.String: TName;

function $StringLength (ref) returns (int);
axiom (forall s:ref :: {$StringLength(s)} 0 <= $StringLength(s));

// for rep fields
function AsRepField(f: Field ref, declaringType: TName) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (forall h: HeapType, o: ref, f: Field ref, T: TName  ::  {h[o,AsRepField(f,T)]}  IsHeap(h) && h[o,AsRepField(f,T)] != null  ==>  h[h[o,AsRepField(f,T)], $ownerRef] == o && h[h[o,AsRepField(f,T)], $ownerFrame] == T);
#endif

// for peer fields
function AsPeerField(f: Field ref) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (forall h: HeapType, o: ref, f: Field ref  ::  {h[o,AsPeerField(f)]}  IsHeap(h) && h[o,AsPeerField(f)] != null  ==>  h[h[o,AsPeerField(f)], $ownerRef] == h[o, $ownerRef] && h[h[o,AsPeerField(f)], $ownerFrame] == h[o, $ownerFrame]);
#endif

// for ElementsRep fields
function AsElementsRepField(f: Field ref, declaringType: TName, position: int) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (forall h: HeapType, o: ref, f: Field ref, T: TName, i: int  ::  {h[o,AsElementsRepField(f,T,i)]}  IsHeap(h) && h[o,AsElementsRepField(f,T,i)] != null  ==>  h[$ElementProxy(h[o,AsElementsRepField(f,T,i)],i), $ownerRef] == o && h[$ElementProxy(h[o,AsElementsRepField(f,T,i)],i), $ownerFrame] == T);
#endif

// for ElementsPeer fields
function AsElementsPeerField(f: Field ref, position: int) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (forall h: HeapType, o: ref, f: Field ref, i: int  ::  {h[o,AsElementsPeerField(f,i)]}  IsHeap(h) && h[o,AsElementsPeerField(f,i)] != null  ==>  h[$ElementProxy(h[o,AsElementsPeerField(f,i)],i), $ownerRef] == h[o, $ownerRef] && h[$ElementProxy(h[o,AsElementsPeerField(f,i)],i), $ownerFrame] == h[o, $ownerFrame]);
#endif



// committed fields are fully valid
#if ExperimentalObjectModel
// this mimics what was here before, alternative: move the inner quantification
// to the outer quantification and add "h[o,$validfor][T]" to the trigger as a multi-trigger
// the above is actually the current encoding!

//axiom (forall h:HeapType, o:ref ::  {h[h[o,$ownerRef], $validfor][h[o, $ownerFrame]] }  
//   IsHeap(h) && h[o,$ownerFrame] != $PeerGroupPlaceholder && h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
//   (forall T:TName :: {h[o,$validfor][T]} h[o,$validfor][T]) && h[o, $inv] == $typeof(o));

//axiom (forall h:HeapType, o:ref ::  {h[h[o,$ownerRef], $validfor][h[o, $ownerFrame]]} {h[o,$validfor]}  
//   IsHeap(h) && h[o,$ownerFrame] != $PeerGroupPlaceholder && h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
//   (forall T:TName :: {h[o,$validfor][T]} h[o,$validfor][T]) && h[o, $inv] == $typeof(o));

axiom (forall h:HeapType, o:ref, T:TName :: {h[o,$validfor][T]}   
  IsHeap(h) && h[o,$ownerFrame] != $PeerGroupPlaceholder && h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
   h[o,$validfor][T] && h[o, $inv] == $typeof(o));

// This axiom might help with some triggering of the one above, further tests needed!
axiom (forall h:HeapType, o:ref :: {h[o, $inv]}   
  IsHeap(h) && h[o,$ownerFrame] != $PeerGroupPlaceholder && h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
   h[o, $inv] == $typeof(o));

#elsif !TrivialObjectModel
axiom (forall h:HeapType, o:ref  ::  {h[h[o,$ownerRef], $inv] <: h[o, $ownerFrame] }  
   IsHeap(h) && h[o,$ownerFrame] != $PeerGroupPlaceholder && h[h[o,$ownerRef], $inv] <: h[o, $ownerFrame]  && h[h[o,$ownerRef], $localinv] != $BaseClass(h[o, $ownerFrame])  ==>  
   h[o,$inv] == $typeof(o) && h[o,$localinv] == $typeof(o));

#endif

// The following procedure sets the owner of o and all its peers to (ow,fr).
// It expects o != null && o.$ownerFrame==$PeerGroupPlaceholder, but this condition is checked at the call site.
procedure $SetOwner(o: ref, ow: ref, fr: TName);
#if !TrivialObjectModel
  modifies $Heap;
  ensures (forall<alpha> p: ref, F: Field alpha ::
      { $Heap[p, F] }
      (F != $ownerRef && F != $ownerFrame) ||
      old($Heap[p, $ownerRef] != $Heap[o, $ownerRef]) ||
      old($Heap[p, $ownerFrame] != $Heap[o, $ownerFrame])
      ==>  old($Heap[p, F]) == $Heap[p, F]);
  ensures (forall p: ref  ::
      { $Heap[p, $ownerRef] }
      { $Heap[p, $ownerFrame] }
      old($Heap[p, $ownerRef] == $Heap[o, $ownerRef]) &&
      old($Heap[p, $ownerFrame] == $Heap[o, $ownerFrame])
      ==>  $Heap[p, $ownerRef] == ow  &&  $Heap[p, $ownerFrame] == fr);
  free ensures $HeapSucc(old($Heap), $Heap);
#endif

// The following procedure is called for "o.f = e;" where f is a rep field declared in a class T:
procedure $UpdateOwnersForRep(o: ref, T: TName, e: ref);
#if !TrivialObjectModel
  modifies $Heap;
  ensures (forall<alpha> p: ref, F: Field alpha  ::
      { $Heap[p, F] }
      (F != $ownerRef && F != $ownerFrame) ||
      old($Heap[p, $ownerRef] != $Heap[e, $ownerRef]) ||
      old($Heap[p, $ownerFrame] != $Heap[e, $ownerFrame])
      ==>  old($Heap[p, F]) == $Heap[p, F]);
  ensures e == null  ==>  $Heap == old($Heap);
  ensures e != null  ==>  (forall p: ref  ::
      { $Heap[p, $ownerRef] }
      { $Heap[p, $ownerFrame] }
      old($Heap[p, $ownerRef] == $Heap[e, $ownerRef]) &&
      old($Heap[p, $ownerFrame] == $Heap[e, $ownerFrame])
      ==>  $Heap[p, $ownerRef] == o  &&  $Heap[p, $ownerFrame] == T);
  free ensures $HeapSucc(old($Heap), $Heap);
#endif

// The following procedure is called for "c.f = d;" where f is a peer field:
procedure $UpdateOwnersForPeer(c: ref, d: ref);
#if !TrivialObjectModel
  modifies $Heap;
  ensures (forall<alpha> p: ref, F: Field alpha  ::
      { $Heap[p, F] }
      (F != $ownerRef && F != $ownerFrame) ||
      old($Heap[p, $ownerRef] != $Heap[d, $ownerRef] || $Heap[p, $ownerFrame] != $Heap[d, $ownerFrame])
      ==>  old($Heap[p, F]) == $Heap[p, F]);
  ensures d == null  ==>  $Heap == old($Heap);
  ensures d != null  ==>  (forall p: ref  ::
      { $Heap[p, $ownerRef] }
      { $Heap[p, $ownerFrame] }
      old($Heap[p, $ownerRef] == $Heap[d, $ownerRef] && $Heap[p, $ownerFrame] == $Heap[d, $ownerFrame])
      ==>
      $Heap[p, $ownerRef] == old($Heap)[c, $ownerRef] &&
      $Heap[p, $ownerFrame] == old($Heap)[c, $ownerFrame]);
  free ensures $HeapSucc(old($Heap), $Heap);
#endif


#if !TrivialObjectModel
// Intuitively, the $FirstConsistentOwner field of an object is defined as the closest
// transitive owner that is consistent.  The field is defined if the object is committed.

const unique $FirstConsistentOwner: Field ref;
#endif

function $AsPureObject(ref) returns (ref);  // used only for triggering
function ##FieldDependsOnFCO<alpha>(o: ref, f: Field alpha, ev: exposeVersionType) returns (alpha);

#if !TrivialObjectModel
// The following axiom say that for any committed object o, each field of o is determined
// by the exposeVersion of o's first consistent owner.

#if FCOAxiom_None
#elsif FCOAxiom_ExposeVersion_Only
axiom (forall o: ref, h: HeapType  ::
  { h[$AsPureObject(o), $exposeVersion] }
  IsHeap(h) &&
  o != null && h[o, $allocated] == true && $AsPureObject(o) == o &&
  h[o, $ownerFrame] != $PeerGroupPlaceholder && 
#if ExperimentalObjectModel
  h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] 
#else
  h[h[o, $ownerRef], $inv] <: h[o, $ownerFrame] &&
  h[h[o, $ownerRef], $localinv] != $BaseClass(h[o, $ownerFrame])
#endif
  ==>
  h[o, $exposeVersion] == ##FieldDependsOnFCO(o, $exposeVersion, h[h[o, $FirstConsistentOwner], $exposeVersion]));
#else
axiom (forall<alpha> o: ref, f: Field alpha, h: HeapType  ::
  { h[$AsPureObject(o), f] }
  IsHeap(h) &&
  o != null && h[o, $allocated] == true && $AsPureObject(o) == o &&
  h[o, $ownerFrame] != $PeerGroupPlaceholder && 
#if ExperimentalObjectModel
  h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] 
#else
  h[h[o, $ownerRef], $inv] <: h[o, $ownerFrame] &&
  h[h[o, $ownerRef], $localinv] != $BaseClass(h[o, $ownerFrame])
#endif
  ==>
  h[o, f] == ##FieldDependsOnFCO(o, f, h[h[o, $FirstConsistentOwner], $exposeVersion]));
#endif

axiom (forall o: ref, h: HeapType  ::
  { h[o, $FirstConsistentOwner] }
  IsHeap(h) &&
  o != null && h[o, $allocated] == true &&
  h[o, $ownerFrame] != $PeerGroupPlaceholder && 
#if ExperimentalObjectModel
  h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] 
#else
  h[h[o, $ownerRef], $inv] <: h[o, $ownerFrame] &&
  h[h[o, $ownerRef], $localinv] != $BaseClass(h[o, $ownerFrame])
#endif
  ==>
  h[o, $FirstConsistentOwner] != null &&
  h[h[o, $FirstConsistentOwner], $allocated] == true &&
  // ! h[h[o, $FirstConsistentOwner], Committed]
  (h[h[o, $FirstConsistentOwner], $ownerFrame] == $PeerGroupPlaceholder ||
#if ExperimentalObjectModel
   !(h[h[h[o, $FirstConsistentOwner],$ownerRef],$validfor][h[h[o, $FirstConsistentOwner], $ownerFrame]]) ));
#else
   !(h[h[h[o, $FirstConsistentOwner], $ownerRef], $inv] <: h[h[o, $FirstConsistentOwner], $ownerFrame]) ||
   h[h[h[o, $FirstConsistentOwner], $ownerRef], $localinv] == $BaseClass(h[h[o, $FirstConsistentOwner], $ownerFrame])));
#endif
#endif

//---------- Boxed and unboxed values

// Unboxing is functional, but boxing is not
function Box<T>(T, ref) returns (ref);
function Unbox<T>(ref) returns (T);

// ...nevertheless, we still need a function that returns a new box.  It would be unsound to always
// return the same value, since each box operation at run time can return a newly allocated value.
// For soundness, we therefore need to add wrap applications of the BoxFunc function into calls to NewInstance, and be sure to
// pass in different values with each invocation of NewInstance.  The way we do that is described near
// the translation of the Box expression.
type NondetType;
function MeldNondets<a>(NondetType, a) returns (NondetType);
function BoxFunc<T>(value: T, typ: TName) returns (boxedValue: ref);
function AllocFunc(typ: TName) returns (newValue: ref);
function NewInstance(object: ref, occurrence: NondetType, activity: ActivityType) returns (newInstance: ref);

axiom (forall<T> value: T, typ: TName, occurrence: NondetType, activity: ActivityType ::
  { NewInstance(BoxFunc(value, typ), occurrence, activity) }
  Box(value, NewInstance(BoxFunc(value, typ), occurrence, activity)) == NewInstance(BoxFunc(value, typ), occurrence, activity) &&
  UnboxedType(NewInstance(BoxFunc(value, typ), occurrence, activity)) == typ);

// Sometimes boxing is just the identity function: namely when its argument is a reference type 
axiom (forall x:ref, typ : TName, occurrence: NondetType, activity : ActivityType :: 
                  !$IsValueType(UnboxedType(x))
              ==> NewInstance(BoxFunc(x,typ), occurrence,activity) == x);

// For simplicity, we track boxed values stored to locals, not those stored into the heap.
axiom (forall<T> x: T, p: ref ::  {Unbox(Box(x,p)): T}  Unbox(Box(x,p)) == x);

function UnboxedType(ref) returns (TName);

// Boxes are always consistent
#if ExperimentalObjectModel
axiom (forall p: ref ::  {$IsValueType(UnboxedType(p))}  $IsValueType(UnboxedType(p))  ==>
  (forall<T> heap: HeapType, x: T ::  {heap[Box(x,p),$inv]} {heap[Box(x,p),$validfor]}  IsHeap(heap)  ==>
    heap[Box(x,p),$inv] == $typeof(Box(x,p)) && (forall T: TName :: {heap[Box(x,p),$validfor][T]} heap[Box(x,p),$validfor][T])));
#elsif !TrivialObjectModel
axiom (forall p: ref ::  {$IsValueType(UnboxedType(p))}  $IsValueType(UnboxedType(p))  ==>
  (forall<T> heap: HeapType, x: T ::  {heap[Box(x,p),$inv]}  IsHeap(heap)  ==>
    heap[Box(x,p),$inv] == $typeof(Box(x,p)) && heap[Box(x,p),$localinv] == $typeof(Box(x,p))));
#endif

// For reference types, boxing returns the reference
axiom (forall<T> x:T, p:ref ::  {UnboxedType(Box(x,p)) <: System.Object}  UnboxedType(Box(x,p)) <: System.Object && Box(x,p) == p  ==>  x == p);

// BoxTester is the value type equivalent of $As
function BoxTester(p:ref, typ: TName) returns (ref);
axiom (forall p:ref, typ: TName ::  {BoxTester(p,typ)}  UnboxedType(p) == typ  <==>  BoxTester(p,typ) != null);
axiom (forall p:ref, typ: TName ::  {BoxTester(p,typ)}  BoxTester(p,typ) != null  ==> (forall<T> ::  Box(Unbox(p): T, p) == p));

// We treat each value x whose type is a type parameter T as a references; that is, the bytecode translator
// gives x the Boogie type ref.  When verifying the generic code, we consider all possible instantiations
// of T; in other words, T is treated parametrically.  Up to a point.  If the generic code performs a type
// test on the x of type T, for example checking if it is of type System.Int32, then the bytecode translation
// would need to treat x as being a Boogie int.  But x can't be both a ref and an int.  Instead, we think
// of the ref x as being a disguise for the int x.  Such disguises form a bijection, which the following
// axioms model by the two functions BoxDisguise and UnBoxDisguise.  That is, these functions essentially
// say that there exists a unique value of the type U (like int) that corresponds to the disguise x of type ref.
function BoxDisguise<U>(U) returns (ref);
function UnBoxDisguise<U>(ref) returns (U);
axiom (forall<U> x: ref, p: ref :: { Unbox(Box(x, p)):U }  Box(x,p) == p  ==>
  Unbox(Box(x, p)):U == UnBoxDisguise(x) &&
  BoxDisguise(Unbox(Box(x, p)):U) == x);


axiom (forall typ: TName, occurrence: NondetType, activity: ActivityType ::
  { NewInstance(AllocFunc(typ), occurrence, activity) }
  $typeof(NewInstance(AllocFunc(typ), occurrence, activity)) == typ &&
  NewInstance(AllocFunc(typ), occurrence, activity) != null);

axiom (forall typ: TName, occurrence: NondetType, activity: ActivityType, heap: HeapType ::
  {heap[NewInstance(AllocFunc(typ), occurrence, activity),$allocated]}  IsHeap(heap)  ==>
    heap[NewInstance(AllocFunc(typ), occurrence, activity),$allocated]);


//---------- Various sized integers

const unique System.SByte : TName;  // sbyte
axiom $IsValueType(System.SByte);
const unique System.Byte : TName;  // byte
axiom $IsValueType(System.Byte);
const unique System.Int16 : TName;  //short
axiom $IsValueType(System.Int16);
const unique System.UInt16 : TName;  // ushort
axiom $IsValueType(System.UInt16);
const unique System.Int32 : TName;  // int
axiom $IsValueType(System.Int32);
const unique System.UInt32 : TName;  // uint
axiom $IsValueType(System.UInt32);
const unique System.Int64 : TName;  // long
axiom $IsValueType(System.Int64);
const unique System.UInt64 : TName;  // ulong
axiom $IsValueType(System.UInt64);
const unique System.Char : TName;  // char
axiom $IsValueType(System.Char);
const unique System.UIntPtr : TName;
axiom $IsValueType(System.UIntPtr);
const unique System.IntPtr : TName;
axiom $IsValueType(System.IntPtr);

function InRange(i: int, T: TName) returns (bool);
axiom (forall i:int :: InRange(i, System.SByte)  <==>  -128 <= i && i < 128);
axiom (forall i:int :: InRange(i, System.Byte)  <==>  0 <= i && i < 256);
axiom (forall i:int :: InRange(i, System.Int16)  <==>  -32768 <= i && i < 32768);
axiom (forall i:int :: InRange(i, System.UInt16)  <==>  0 <= i && i < 65536);
axiom (forall i:int :: InRange(i, System.Int32)  <==>  -2147483648 <= i && i <= 2147483647);
axiom (forall i:int :: InRange(i, System.UInt32)  <==>  0 <= i && i <= 4294967295);
axiom (forall i:int :: InRange(i, System.Int64)  <==>  -9223372036854775808 <= i && i <= 9223372036854775807);
axiom (forall i:int :: InRange(i, System.UInt64)  <==>  0 <= i && i <= 18446744073709551615);
axiom (forall i:int :: InRange(i, System.Char)  <==>  0 <= i && i < 65536);


//---------- Type conversions and sizes


function $IntToInt(val: int, fromType: TName, toType: TName) returns (int);
function $IntToReal(val: int, fromType: TName, toType: TName) returns (real);
function $RealToInt(val: real, fromType: TName, toType: TName) returns (int);
function $RealToReal(val: real, fromType: TName, toType: TName) returns (real);

axiom (forall z: int, B: TName, C: TName :: InRange(z, C) ==> $IntToInt(z, B, C) == z);

function $SizeIs (TName, int) returns (bool); // SizeIs(T,n) means that n = sizeof(T)



//------------ Formula/term operators

function $IfThenElse<a>(bool, a, a) returns (a);

axiom (forall<a> b:bool, x:a, y:a :: {$IfThenElse(b,x,y)} b ==>  $IfThenElse(b,x,y) == x);
axiom (forall<a> b:bool, x:a, y:a :: {$IfThenElse(b,x,y)} !b ==>  $IfThenElse(b,x,y) == y);

//------------ Bit-level operators

function #neg (int) returns (int);
function #and (int, int) returns (int);
function #or (int, int) returns (int);
function #xor (int, int) returns (int);
function #shl (int, int) returns (int);
function #shr (int, int) returns (int);

function #rneg(real) returns (real);
function #radd(real, real) returns (real);
function #rsub(real, real) returns (real);
function #rmul(real, real) returns (real);
function #rdiv(real, real) returns (real);
function #rmod(real, real) returns (real);
function #rLess(real, real) returns (bool);
function #rAtmost(real, real) returns (bool);
function #rEq(real, real) returns (bool);
function #rNeq(real, real) returns (bool);
function #rAtleast(real, real) returns (bool);
function #rGreater(real, real) returns (bool);


//----------- Properties of operators

// the connection between % and /
axiom (forall x:int, y:int :: {x % y} {x / y}  x % y == x - x / y * y);

// remainder is C# is complicated, because division rounds toward 0
axiom (forall x:int, y:int :: {x % y}  0 <= x && 0 < y  ==>  0 <= x % y  &&  x % y < y);
axiom (forall x:int, y:int :: {x % y}  0 <= x && y < 0  ==>  0 <= x % y  &&  x % y < -y);
axiom (forall x:int, y:int :: {x % y}  x <= 0 && 0 < y  ==>  -y < x % y  &&  x % y <= 0);
axiom (forall x:int, y:int :: {x % y}  x <= 0 && y < 0  ==>  y < x % y  &&  x % y <= 0);

axiom (forall x:int, y:int :: {(x + y) % y}  0 <= x && 0 <= y  ==>  (x + y) % y == x % y);
// do we need this symmetric one, too?
axiom (forall x:int, y:int :: {(y + x) % y}  0 <= x && 0 <= y  ==>  (y + x) % y == x % y);
axiom (forall x:int, y:int :: {(x - y) % y}  0 <= x-y && 0 <= y  ==>  (x - y) % y == x % y);

// the following axiom prevents a matching loop in Simplify
// axiom (forall x:int, y:int :: {x * y / y * y}  x * y / y * y == x * y);

// the following axiom has some unfortunate matching, but it does state a property about % that
// is sometime useful
axiom (forall a: int, b: int, d: int :: { a % d, b % d } 2 <= d && a % d == b % d && a < b  ==>  a + d <= b);

#if ArithDistributionAxioms
//  These axioms provide good functionality, but in some cases they can be very expensive
// distributions of * and +/-
axiom (forall x: int, y: int, z: int ::  { (x+y)*z }  (x+y)*z == x*z + y*z);
axiom (forall x: int, y: int, z: int ::  { (x-y)*z }  (x-y)*z == x*z - y*z);
axiom (forall x: int, y: int, z: int ::  { z*(x+y) }  z*(x+y) == z*x + z*y);
axiom (forall x: int, y: int, z: int ::  { z*(x-y) }  z*(x-y) == z*x - z*y);
#endif

axiom (forall x: int, y: int :: { #and(x,y) }  #and(x,y) == #and(y,x));
axiom (forall x: int, y: int :: { #or(x,y) }  #or(x,y) == #or(y,x));

axiom (forall x: int, y: int :: { #and(x,y) }  0 <= x || 0 <= y  ==>  0 <= #and(x,y));
axiom (forall x: int, y: int :: { #or(x,y) }  0 <= x && 0 <= y  ==>  0 <= #or(x,y) && #or(x,y) <= x + y);

axiom (forall x: int :: { #and(x,-1) }  #and(x,-1) == x);
axiom (forall x: int :: { #and(x,0) }  #and(x,0) == 0);
axiom (forall x: int :: { #or(x,-1) }  #or(x,-1) == -1);
axiom (forall x: int :: { #or(x,0) }  #or(x,0) == x);

axiom (forall i:int :: {#shl(i,0)} #shl(i,0) == i);
axiom (forall i:int, j:int :: {#shl(i,j)}  1 <= j ==> #shl(i,j) == #shl(i,j-1) * 2);
axiom (forall i:int, j:int :: {#shl(i,j)} 0 <= i && i < 32768 && 0 <= j && j <= 16  ==>  0 <= #shl(i, j) && #shl(i, j) <= 2147483647);

axiom (forall i:int :: {#shr(i,0)} #shr(i,0) == i);
axiom (forall i:int, j:int :: {#shr(i,j)} 1 <= j ==> #shr(i,j) == #shr(i,j-1) / 2);


function #min(int, int) returns (int);
function #max(int, int) returns (int);
axiom (forall x: int, y: int :: { #min(x,y) } (#min(x,y) == x || #min(x,y) == y) && #min(x,y) <= x && #min(x,y) <= y);
axiom (forall x: int, y: int :: { #max(x,y) } (#max(x,y) == x || #max(x,y) == y) && x <= #max(x,y) && y <= #max(x,y));


//---------- Properties of String (Literals)

function #System.String.IsInterned$System.String$notnull(HeapType, ref) returns (ref);
function #System.String.Equals$System.String(HeapType, ref, ref) returns (bool);
function #System.String.Equals$System.String$System.String(HeapType, ref, ref) returns (bool);
function ##StringEquals(ref, ref) returns (bool);

// two names for String.Equals
axiom (forall h: HeapType, a: ref, b: ref ::
 { #System.String.Equals$System.String(h, a, b) }
 #System.String.Equals$System.String(h, a, b) == #System.String.Equals$System.String$System.String(h, a, b));

// String.Equals is independent of the heap, and it is reflexive and commutative
axiom (forall h: HeapType, a: ref, b: ref ::
 { #System.String.Equals$System.String$System.String(h, a, b) }
 #System.String.Equals$System.String$System.String(h, a, b) == ##StringEquals(a, b) &&
 #System.String.Equals$System.String$System.String(h, a, b) == ##StringEquals(b, a) &&
 (a == b  ==>  ##StringEquals(a, b)));

// String.Equals is also transitive
axiom (forall a: ref, b: ref, c: ref ::  ##StringEquals(a, b) && ##StringEquals(b, c)  ==>  ##StringEquals(a, c));

// equal strings have the same interned ref
axiom (forall h: HeapType, a: ref, b: ref ::
 { #System.String.Equals$System.String$System.String(h, a, b) }
 a != null && b != null && #System.String.Equals$System.String$System.String(h, a, b)
 ==>
 #System.String.IsInterned$System.String$notnull(h, a) == 
 #System.String.IsInterned$System.String$notnull(h, b));

// ---------------------------------------------------------------
// -- Axiomatization of sequences --------------------------------
// ---------------------------------------------------------------

#if UseSequences
function Ref2Seq(ref) returns (Seq int);

axiom Ref2Seq(##RR.Sequence.Empty()) == Seq#Empty();

axiom (forall x: int ::
  { ##RR.Sequence.Singleton$System.Int32(x) }
  Ref2Seq(##RR.Sequence.Singleton$System.Int32(x)) ==
  Seq#Singleton(x));

axiom (forall s: ref ::
  { ##RR.Sequence.get_Length(s) }
  ##RR.Sequence.get_Length(s) ==
  Seq#Length(Ref2Seq(s)));

axiom (forall s: ref, i: int ::
  { ##RR.Sequence.get_Item$System.Int32(s, i) }
  ##RR.Sequence.get_Item$System.Int32(s, i) ==
  Seq#Index(Ref2Seq(s), i));

axiom (forall s: ref, x: int ::
  { ##RR.Sequence.Contains$System.Int32(s, x) }
  ##RR.Sequence.Contains$System.Int32(s, x) ==
  Seq#Contains(Ref2Seq(s), x));

axiom (forall s: ref, from: int, to: int ::
  { ##RR.Sequence.SubSequence$System.Int32$System.Int32(s, from, to) }
  Ref2Seq(##RR.Sequence.SubSequence$System.Int32$System.Int32(s, from, to)) ==
  Seq#Take(Seq#Drop(Ref2Seq(s), from), to-from));

axiom (forall s: ref, howMany: int ::
  { ##RR.Sequence.Take$System.Int32(s, howMany) }
  Ref2Seq(##RR.Sequence.Take$System.Int32(s, howMany)) ==
  Seq#Take(Ref2Seq(s), howMany));

axiom (forall s: ref, howMany: int ::
  { ##RR.Sequence.Drop$System.Int32(s, howMany) }
  Ref2Seq(##RR.Sequence.Drop$System.Int32(s, howMany)) ==
  Seq#Drop(Ref2Seq(s), howMany));

axiom (forall s: ref, t: ref ::
  { ##RR.Sequence.Concat$RR.Sequence$notnull(s, t) }
  Ref2Seq(##RR.Sequence.Concat$RR.Sequence$notnull(s, t)) ==
  Seq#Append(Ref2Seq(s), Ref2Seq(t)));

axiom (forall s: ref, t: ref ::
  { ##RR.Sequence.op_Equality$RR.Sequence$notnull$RR.Sequence$notnull(s, t) }
  ##RR.Sequence.op_Equality$RR.Sequence$notnull$RR.Sequence$notnull(s, t) ==
  Seq#Equal(Ref2Seq(s), Ref2Seq(t)));


type Seq T;

function Seq#Length<T>(Seq T) returns (int);
axiom (forall<T> s: Seq T :: { Seq#Length(s) } 0 <= Seq#Length(s));

function Seq#Empty<T>() returns (Seq T);
axiom (forall<T> :: Seq#Length(Seq#Empty(): Seq T) == 0);
axiom (forall<T> s: Seq T :: { Seq#Length(s) } Seq#Length(s) == 0 ==> s == Seq#Empty());

function Seq#Singleton<T>(T) returns (Seq T);
axiom (forall<T> t: T :: { Seq#Length(Seq#Singleton(t)) } Seq#Length(Seq#Singleton(t)) == 1);

function Seq#Build<T>(s: Seq T, index: int, val: T, newLength: int) returns (Seq T);
axiom (forall<T> s: Seq T, i: int, v: T, len: int :: { Seq#Length(Seq#Build(s,i,v,len)) }
  0 <= len ==> Seq#Length(Seq#Build(s,i,v,len)) == len);

function Seq#Append<T>(Seq T, Seq T) returns (Seq T);
axiom (forall<T> s0: Seq T, s1: Seq T :: { Seq#Length(Seq#Append(s0,s1)) }
  Seq#Length(Seq#Append(s0,s1)) == Seq#Length(s0) + Seq#Length(s1));

function Seq#Index<T>(Seq T, int) returns (T);
axiom (forall<T> t: T :: { Seq#Index(Seq#Singleton(t), 0) } Seq#Index(Seq#Singleton(t), 0) == t);
axiom (forall<T> s0: Seq T, s1: Seq T, n: int :: { Seq#Index(Seq#Append(s0,s1), n) }
  (n < Seq#Length(s0) ==> Seq#Index(Seq#Append(s0,s1), n) == Seq#Index(s0, n)) &&
  (Seq#Length(s0) <= n ==> Seq#Index(Seq#Append(s0,s1), n) == Seq#Index(s1, n - Seq#Length(s0))));
axiom (forall<T> s: Seq T, i: int, v: T, len: int, n: int :: { Seq#Index(Seq#Build(s,i,v,len),n) }
  0 <= n && n < len ==>
    (i == n ==> Seq#Index(Seq#Build(s,i,v,len),n) == v) &&
    (i != n ==> Seq#Index(Seq#Build(s,i,v,len),n) == Seq#Index(s,n)));

function Seq#Contains<T>(Seq T, T) returns (bool);
axiom (forall<T> s: Seq T, x: T :: { Seq#Contains(s,x) }
  Seq#Contains(s,x) <==>
    (exists i: int :: { Seq#Index(s,i) } 0 <= i && i < Seq#Length(s) && Seq#Index(s,i) == x));
axiom (forall x: ref ::
  { Seq#Contains(Seq#Empty(), x) }
  !Seq#Contains(Seq#Empty(), x));
axiom (forall<T> s0: Seq T, s1: Seq T, x: T ::
  { Seq#Contains(Seq#Append(s0, s1), x) }
  Seq#Contains(Seq#Append(s0, s1), x) <==>
    Seq#Contains(s0, x) || Seq#Contains(s1, x));
axiom (forall<T> s: Seq T, i: int, v: T, len: int, x: T ::
  { Seq#Contains(Seq#Build(s, i, v, len), x) }
  Seq#Contains(Seq#Build(s, i, v, len), x) <==>
    (0 <= i && i < len && x == v)  ||  
    (exists j: int :: { Seq#Index(s,j) } 0 <= j && j < Seq#Length(s) && j < len && j!=i && Seq#Index(s,j) == x));
axiom (forall<T> s: Seq T, n: int, x: T ::
  { Seq#Contains(Seq#Take(s, n), x) }
  Seq#Contains(Seq#Take(s, n), x) <==>
    (exists i: int :: { Seq#Index(s, i) }
      0 <= i && i < n && i < Seq#Length(s) && Seq#Index(s, i) == x));
axiom (forall<T> s: Seq T, n: int, x: T ::
  { Seq#Contains(Seq#Drop(s, n), x) }
  Seq#Contains(Seq#Drop(s, n), x) <==>
    (exists i: int :: { Seq#Index(s, i) }
      0 <= n && n <= i && i < Seq#Length(s) && Seq#Index(s, i) == x));

function Seq#Equal<T>(Seq T, Seq T) returns (bool);
axiom (forall<T> s0: Seq T, s1: Seq T :: { Seq#Equal(s0,s1) }
  Seq#Equal(s0,s1) <==>
    Seq#Length(s0) == Seq#Length(s1) &&
    (forall j: int :: { Seq#Index(s0,j) } { Seq#Index(s1,j) }
        0 <= j && j < Seq#Length(s0) ==> Seq#Index(s0,j) == Seq#Index(s1,j)));
axiom(forall<T> a: Seq T, b: Seq T :: { Seq#Equal(a,b) }  // extensionality axiom for sequences
  Seq#Equal(a,b) ==> a == b);

function Seq#SameUntil<T>(Seq T, Seq T, int) returns (bool);
axiom (forall<T> s0: Seq T, s1: Seq T, n: int :: { Seq#SameUntil(s0,s1,n) }
  Seq#SameUntil(s0,s1,n) <==>
    (forall j: int :: { Seq#Index(s0,j) } { Seq#Index(s1,j) }
        0 <= j && j < n ==> Seq#Index(s0,j) == Seq#Index(s1,j)));

function Seq#Take<T>(Seq T, howMany: int) returns (Seq T);
axiom (forall<T> s: Seq T, n: int :: { Seq#Length(Seq#Take(s,n)) }
  0 <= n ==>
    (n <= Seq#Length(s) ==> Seq#Length(Seq#Take(s,n)) == n) &&
    (Seq#Length(s) < n ==> Seq#Length(Seq#Take(s,n)) == Seq#Length(s)));
axiom (forall<T> s: Seq T, n: int, j: int :: { Seq#Index(Seq#Take(s,n), j) }
  0 <= j && j < n && j < Seq#Length(s) ==>
    Seq#Index(Seq#Take(s,n), j) == Seq#Index(s, j));

function Seq#Drop<T>(Seq T, howMany: int) returns (Seq T);
axiom (forall<T> s: Seq T, n: int :: { Seq#Length(Seq#Drop(s,n)) }
  0 <= n ==>
    (n <= Seq#Length(s) ==> Seq#Length(Seq#Drop(s,n)) == Seq#Length(s) - n) &&
    (Seq#Length(s) < n ==> Seq#Length(Seq#Drop(s,n)) == 0));
axiom (forall<T> s: Seq T, n: int, j: int :: { Seq#Index(Seq#Drop(s,n), j) }
  0 <= n && 0 <= j && j < Seq#Length(s)-n ==>
    Seq#Index(Seq#Drop(s,n), j) == Seq#Index(s, j+n));
#endif

// ************** END PRELUDE **************
