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
axiom (∀ $oi: ref, $h: HeapType • { $Inv($h, $oi, System.Object) } $Inv($h, $oi, System.Object));

// array types class invariants
axiom (∀ $oi: ref, $h: HeapType, T: TName • { $Inv($h, $oi, T), T <: System.Array } T <: System.Array ==> $Inv($h, $oi, T));

#elsif ExperimentalObjectModel
// the name of the most derived class for which an object invariant holds
const unique $inv : Field TName;

// array, indexed by class names, that keeps a boolean showing the invariant holds for that class on the object given
const unique $validfor: Field [TName]bool;

axiom (∀ h: HeapType, o: ref, T: TName • {h[o, $validfor][T]} 
    IsHeap(h) ∧ o ≠ null ∧  $typeof(o) <: T ∧ $BaseClass(T) <: h[o, $inv] ==> ¬h[o, $validfor][T]);
axiom (∀ h: HeapType, o: ref • {h[o,$inv]} 
    IsHeap(h) ∧ o ≠ null && h[o,$validfor][$typeof(o)] ==> h[o,$inv] == $typeof(o));

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
axiom (∀ c: TName • {ClassRepr(c)} ClassReprInv(ClassRepr(c)) == c);
axiom (∀ T: TName • ¬($typeof(ClassRepr(T)) <: System.Object));
axiom (∀ T: TName • ClassRepr(T) ≠ null);
#if !TrivialObjectModel
axiom (∀ T: TName, h: HeapType • {h[ClassRepr(T),$ownerFrame]} IsHeap(h) ⇒ h[ClassRepr(T),$ownerFrame] == $PeerGroupPlaceholder);
#endif

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
axiom ¬(IncludeInMainFrameCondition($inv));
#if ExperimentalObjectModel
axiom ¬(IncludeInMainFrameCondition($validfor));
#else
axiom ¬(IncludeInMainFrameCondition($localinv));
#endif
axiom IncludeInMainFrameCondition($ownerRef);
axiom IncludeInMainFrameCondition($ownerFrame);
#endif
axiom IncludeInMainFrameCondition($exposeVersion);
#if !TrivialObjectModel
axiom ¬(IncludeInMainFrameCondition($FirstConsistentOwner));
#endif

// indicates a field is static
function IsStaticField<alpha>(f: Field alpha) returns (bool);
axiom ¬IsStaticField($allocated);
axiom ¬IsStaticField($elementsBool) &&
      ¬IsStaticField($elementsInt) &&
      ¬IsStaticField($elementsRef) &&
      ¬IsStaticField($elementsReal) &&
      ¬IsStaticField($elementsStruct);
#if !TrivialObjectModel
axiom ¬IsStaticField($inv);
#if ExperimentalObjectModel
axiom ¬IsStaticField($validfor);
#else
axiom ¬IsStaticField($localinv);
#endif
#endif
axiom ¬IsStaticField($exposeVersion);

// indicates if a is included in modifies o.* and o.**
function $IncludedInModifiesStar<alpha>(f: Field alpha) returns (bool);
#if !TrivialObjectModel
axiom ¬$IncludedInModifiesStar($ownerRef);
axiom ¬$IncludedInModifiesStar($ownerFrame);
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

axiom (∀<alpha> A: Elements alpha, i: int, x: alpha • ArrayGet(ArraySet(A, i, x), i) == x);
axiom (∀<alpha> A: Elements alpha, i: int, j: int, x: alpha • i ≠ j  ⇒ ArrayGet(ArraySet(A, i, x), j) == ArrayGet(A, j)); 

// the indices of multi-dimensional arrays are built up one dimension at a time
function ArrayIndex(arr: ref, dim: int, indexAtDim: int, remainingIndexContribution: int) returns (int);
// the expressions built up are injective in the indices
function ArrayIndexInvX(arrayIndex: int) returns (indexAtDim: int);
function ArrayIndexInvY(arrayIndex: int) returns (remainingIndexContribution: int);
axiom (∀ a:ref, d:int, x: int, y: int •  {ArrayIndex(a,d,x,y)}  ArrayIndexInvX(ArrayIndex(a,d,x,y)) == x);
axiom (∀ a:ref, d:int, x: int, y: int •  {ArrayIndex(a,d,x,y)}  ArrayIndexInvY(ArrayIndex(a,d,x,y)) == y);

axiom (∀ a:ref, i:int, heap:HeapType •
   { ArrayGet(heap[a, $elementsInt], i) }
   IsHeap(heap) ⇒  InRange(ArrayGet(heap[a, $elementsInt], i), $ElementType($typeof(a))));
axiom (∀ a:ref, i:int, heap:HeapType •
    { $typeof(ArrayGet(heap[a, $elementsRef], i)) }
    IsHeap(heap) ∧ ArrayGet(heap[a, $elementsRef], i) ≠ null  ⇒
    $typeof(ArrayGet(heap[a, $elementsRef], i)) <: $ElementType($typeof(a)));
axiom (∀ a:ref, T:TName, i:int, r:int, heap:HeapType •
    { $typeof(a) <: NonNullRefArray(T, r), ArrayGet(heap[a, $elementsRef], i) }
    IsHeap(heap) ∧ $typeof(a) <: NonNullRefArray(T,r)  ⇒  ArrayGet(heap[a, $elementsRef], i) ≠ null);

//------------ Array properties: rank, length, dimensions, upper and lower bounds

function $Rank (ref) returns (int); 
axiom (∀ a:ref • 1 ≤ $Rank(a));
axiom (∀ a:ref, T:TName, r:int • {$typeof(a) <: RefArray(T,r)} a ≠ null ∧ $typeof(a) <: RefArray(T,r)  ⇒ $Rank(a) == r);
axiom (∀ a:ref, T:TName, r:int • {$typeof(a) <: NonNullRefArray(T,r)} a ≠ null ∧ $typeof(a) <: NonNullRefArray(T,r)  ⇒ $Rank(a) == r);
axiom (∀ a:ref, T:TName, r:int • {$typeof(a) <: ValueArray(T,r)} a ≠ null ∧ $typeof(a) <: ValueArray(T,r)  ⇒ $Rank(a) == r);
axiom (∀ a:ref, T:TName, r:int • {$typeof(a) <: IntArray(T,r)} a ≠ null ∧ $typeof(a) <: IntArray(T,r)  ⇒ $Rank(a) == r);

function $Length (ref) returns (int);
axiom (∀ a:ref • {$Length(a)} 0 ≤ $Length(a) ∧ $Length(a) ≤ 2147483647);

function $DimLength (ref, int) returns (int); // length per dimension up to rank
axiom (∀ a:ref, i:int • 0 ≤ $DimLength(a,i));
// The trigger used in the following axiom is restrictive, so that this disjunction is not
// produced too easily.  Is the trigger perhaps sometimes too restrictive?
axiom (∀ a:ref • { $DimLength(a,0) }  $Rank(a) == 1 ⇒ $DimLength(a,0) == $Length(a));

function $LBound (ref, int) returns (int); 
function $UBound (ref, int) returns (int);
// Right now we only model C# arrays:
axiom (∀ a:ref, i:int • {$LBound(a,i)} $LBound(a,i) == 0);
axiom (∀ a:ref, i:int • {$UBound(a,i)} $UBound(a,i) == $DimLength(a,i)-1);

// Different categories of arrays are different types

type ArrayCategory;
const unique $ArrayCategoryValue: ArrayCategory;
const unique $ArrayCategoryInt: ArrayCategory;
const unique $ArrayCategoryRef: ArrayCategory;
const unique $ArrayCategoryNonNullRef: ArrayCategory;

function $ArrayCategory(arrayType: TName) returns (arrayCategory: ArrayCategory);

axiom (∀ T: TName, ET: TName, r: int • { T <: ValueArray(ET, r) } T <: ValueArray(ET, r) ⇒ $ArrayCategory(T) == $ArrayCategoryValue);
axiom (∀ T: TName, ET: TName, r: int • { T <: IntArray(ET, r) } T <: IntArray(ET, r) ⇒ $ArrayCategory(T) == $ArrayCategoryInt);
axiom (∀ T: TName, ET: TName, r: int • { T <: RefArray(ET, r) } T <: RefArray(ET, r) ⇒ $ArrayCategory(T) == $ArrayCategoryRef);
axiom (∀ T: TName, ET: TName, r: int • { T <: NonNullRefArray(ET, r) } T <: NonNullRefArray(ET, r) ⇒ $ArrayCategory(T) == $ArrayCategoryNonNullRef);

//------------ Array types

const unique System.Array : TName;
axiom System.Array <: System.Object;

function $ElementType(TName) returns (TName);

function ValueArray (elementType:TName, rank:int) returns (TName); 
axiom (∀ T:TName, r:int • {ValueArray(T,r)} ValueArray(T,r) <: ValueArray(T,r) ∧ ValueArray(T,r) <: System.Array);
function IntArray (elementType:TName, rank:int) returns (TName); 
axiom (∀ T:TName, r:int • {IntArray(T,r)} IntArray(T,r) <: IntArray(T,r) ∧ IntArray(T,r) <: System.Array);

function RefArray (elementType:TName, rank:int) returns (TName); 
axiom (∀ T:TName, r:int • {RefArray(T,r)} RefArray(T,r) <: RefArray(T,r) ∧ RefArray(T,r) <: System.Array);
function NonNullRefArray (elementType:TName, rank:int) returns (TName); 
axiom (∀ T:TName, r:int • {NonNullRefArray(T,r)} NonNullRefArray(T,r) <: NonNullRefArray(T,r) ∧ NonNullRefArray(T,r) <: System.Array);
function NonNullRefArrayRaw(array: ref, elementType: TName, rank: int) returns (bool);
axiom (∀ array: ref, elementType: TName, rank: int •  { NonNullRefArrayRaw(array, elementType, rank) }
  NonNullRefArrayRaw(array, elementType, rank)
  ⇒  $typeof(array) <: System.Array ∧ $Rank(array) == rank ∧ elementType <: $ElementType($typeof(array)));

// arrays of references are co-variant
axiom (∀ T:TName, U:TName, r:int • U <: T  ⇒  RefArray(U,r) <: RefArray(T,r));
axiom (∀ T:TName, U:TName, r:int • U <: T  ⇒  NonNullRefArray(U,r) <: NonNullRefArray(T,r));

axiom (∀ A: TName, r: int • $ElementType(ValueArray(A,r)) == A);
axiom (∀ A: TName, r: int • $ElementType(IntArray(A,r)) == A);
axiom (∀ A: TName, r: int • $ElementType(RefArray(A,r)) == A);
axiom (∀ A: TName, r: int • $ElementType(NonNullRefArray(A,r)) == A);

// subtypes of array types
axiom (∀ A: TName, r: int, T: TName •  {T <: RefArray(A,r)} T <: RefArray(A,r)  ⇒  T ≠ A ∧ T == RefArray($ElementType(T),r) ∧ $ElementType(T) <: A);
axiom (∀ A: TName, r: int, T: TName •  {T <: NonNullRefArray(A,r)} T <: NonNullRefArray(A,r)  ⇒  T ≠ A ∧ T == NonNullRefArray($ElementType(T),r) ∧ $ElementType(T) <: A);
axiom (∀ A: TName, r: int, T: TName •  {T <: ValueArray(A, r)} T <: ValueArray(A, r)  ⇒  T == ValueArray(A, r));
axiom (∀ A: TName, r: int, T: TName •  {T <: IntArray(A, r)} T <: IntArray(A, r)  ⇒  T == IntArray(A, r));

// supertypes of array types
axiom (∀ A: TName, r: int, T: TName •  {RefArray(A,r) <: T}  RefArray(A,r) <: T  ⇒  System.Array <: T ∨ (T == RefArray($ElementType(T),r) ∧ A <: $ElementType(T)));
axiom (∀ A: TName, r: int, T: TName •  {NonNullRefArray(A,r) <: T}  NonNullRefArray(A,r) <: T  ⇒  System.Array <: T ∨ (T == NonNullRefArray($ElementType(T),r) ∧ A <: $ElementType(T)));
axiom (∀ A: TName, r: int, T: TName •  {ValueArray(A, r) <: T}  ValueArray(A, r) <: T  ⇒  System.Array <: T ∨ T == ValueArray(A, r));
axiom (∀ A: TName, r: int, T: TName •  {IntArray(A, r) <: T}  IntArray(A, r) <: T  ⇒  System.Array <: T ∨ T == IntArray(A, r));

function $ArrayPtr (elementType:TName) returns (TName); 


//------------ Array and generic element ownership
function $ElementProxy(ref, int) returns (ref);
function $ElementProxyStruct(struct, int) returns (ref);


#if !TrivialObjectModel
axiom (∀ a: ref, i: int, heap: HeapType :: { heap[ArrayGet(heap[a, $elementsRef], i),$ownerRef] } { heap[ArrayGet(heap[a, $elementsRef], i),$ownerFrame] } IsHeap(heap) ∧ $typeof(a) <: System.Array ⇒ ArrayGet(heap[a, $elementsRef], i) == null ∨ $IsImmutable($typeof(ArrayGet(heap[a, $elementsRef], i))) ∨ (heap[ArrayGet(heap[a, $elementsRef], i),$ownerRef] == heap[$ElementProxy(a,-1),$ownerRef] ∧ heap[ArrayGet(heap[a, $elementsRef], i),$ownerFrame] == heap[$ElementProxy(a,-1),$ownerFrame]));
#endif

axiom (∀ a: ref, heap: HeapType :: { IsAllocated(heap,a) } IsHeap(heap) ∧ IsAllocated(heap,a) ∧ $typeof(a) <: System.Array ⇒ IsAllocated(heap, $ElementProxy(a,-1)));

axiom (∀ o: ref, pos: int :: { $typeof($ElementProxy(o,pos)) } $typeof($ElementProxy(o,pos)) == System.Object);
axiom (∀ o: struct, pos: int :: { $typeof($ElementProxyStruct(o,pos)) } $typeof($ElementProxyStruct(o,pos)) == System.Object);


//------------ Encode structs

function $StructGet<alpha>(struct, Field alpha) returns (alpha);

function $StructSet<alpha>(struct, Field alpha, alpha) returns (struct);


axiom (∀<alpha> s: struct, f: Field alpha, x: alpha •  $StructGet($StructSet(s, f, x), f) == x);
		
axiom (∀<alpha,beta> s: struct, f: Field alpha, f': Field beta, x: alpha •  f ≠ f'  ⇒  $StructGet($StructSet(s, f, x), f') == $StructGet(s, f')); 

function ZeroInit(s:struct, typ:TName) returns (bool);
// TODO: ZeroInit needs axiomatization that says the fields of s are 0 or null or ZeroInit, depending on their types


//------------ Encode type information

function $typeof (ref) returns (TName);

function $BaseClass(sub: TName) returns (base: TName);
axiom (∀ T: TName •  { $BaseClass(T) }  T <: $BaseClass(T) ∧  (T != System.Object ==> T != $BaseClass(T)));

// Incomparable subtype axiom:
function AsDirectSubClass(sub: TName, base: TName) returns (sub': TName);
function OneClassDown(sub: TName, base: TName) returns (directSub: TName);
axiom (∀ A: TName, B: TName, C: TName • { C <: AsDirectSubClass(B,A) }  C <: AsDirectSubClass(B,A)  ⇒  OneClassDown(C,A) == B);

// primitive types are unordered in the type ordering
function $IsValueType(TName) returns (bool);
axiom (∀ T: TName • $IsValueType(T)  ⇒  (∀ U: TName •  T <: U  ⇒  T == U) ∧ (∀ U: TName •  U <: T  ⇒  T == U));

const unique System.Boolean: TName;  // bool
axiom $IsValueType(System.Boolean);

// type constructor T[] 
//
const unique System.Object : TName;

// reflection
//
function $IsTokenForType (struct, TName) returns (bool);
function TypeObject (TName) returns (ref); // Corresponds with C# typeof(T)
const unique System.Type : TName;
axiom System.Type <: System.Object;
axiom (∀ T:TName • {TypeObject(T)} $IsNotNull(TypeObject(T), System.Type));
function TypeName(ref) returns (TName);  // the inverse of TypeObject, which is injective
axiom (∀ T:TName • {TypeObject(T)}  TypeName(TypeObject(T)) == T);

function $Is (ref, TName) returns (bool);
axiom (∀ o:ref, T:TName • {$Is(o, T)} $Is(o, T)  ⇔  o == null ∨ $typeof(o) <: T);

function $IsNotNull(ref, TName) returns (bool);
axiom (∀ o:ref, T:TName • {$IsNotNull(o, T)} $IsNotNull(o, T)  ⇔  o ≠ null ∧ $Is(o,T));

// $As(o,T) is to be used only when T denotes a reference type (see also BoxTester).  It returns either o or null.
function $As (ref, TName) returns (ref);
axiom (∀ o:ref, T:TName • $Is(o, T)  ⇒  $As(o, T) == o);
axiom (∀ o:ref, T:TName • ¬ $Is(o, T)  ⇒  $As(o, T) == null);

// Arrays are always valid (but may be committed)
#if ExperimentalObjectModel
axiom (∀ o: ref • {$typeof(o) <: System.Array} o ≠ null ∧ $typeof(o) <: System.Array  ⇒ 
         (∀ h: HeapType • {h[o,$inv]} {h[o,$validfor]} IsHeap(h) ⇒  
            (∀ T: TName • {h[o,$validfor][T]} h[o,$validfor][T]) ∧ 
            h[o,$inv] == $typeof(o)));
#elsif !TrivialObjectModel
axiom (∀ h: HeapType, o: ref • {$typeof(o) <: System.Array, h[o,$inv]} IsHeap(h) ∧ o ≠ null ∧ $typeof(o) <: System.Array  ⇒  h[o,$inv] == $typeof(o) ∧ h[o,$localinv] == $typeof(o));
#endif

//---------- Types and allocation of reachable things

function IsAllocated<alpha>(h: HeapType, o: alpha) returns (bool);

// everything in the range of a proper heap is allocated whenever the domain is
axiom (∀<alpha> h: HeapType, o: ref, f: Field alpha • {IsAllocated(h, h[o,f])} IsHeap(h) ∧ h[o, $allocated]  ⇒  IsAllocated(h, h[o,f]));
axiom (∀ h: HeapType, o: ref, f: Field ref • {h[h[o,f], $allocated]} IsHeap(h) ∧ h[o, $allocated]  ⇒  h[h[o,f], $allocated]);

axiom (∀<alpha> h: HeapType, s: struct, f: Field alpha • {IsAllocated(h, $StructGet(s,f))} IsAllocated(h,s)  ⇒  IsAllocated(h, $StructGet(s,f)));
axiom (∀<alpha> h: HeapType, e: Elements alpha, i: int• {IsAllocated(h, ArrayGet(e,i))} IsAllocated(h,e)  ⇒  IsAllocated(h, ArrayGet(e,i)));

axiom (∀ h: HeapType, o: ref • {h[o, $allocated]}  IsAllocated(h,o)  ⇒  h[o, $allocated]);

axiom (∀ h: HeapType, c:TName • {h[ClassRepr(c), $allocated]} IsHeap(h)  ⇒  h[ClassRepr(c), $allocated]);

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

axiom (∀ f: Field ref, T: TName • {AsNonNullRefField(f,T)}  AsNonNullRefField(f,T)==f  ⇒  AsRefField(f,T)==f);

// fields in the heap are well typed
axiom (∀ h: HeapType, o: ref, f: Field ref, T: TName • {h[o,AsRefField(f,T)]}  IsHeap(h)  ⇒  $Is(h[o,AsRefField(f,T)], T));
axiom (∀ h: HeapType, o: ref, f: Field ref, T: TName • {h[o,AsNonNullRefField(f,T)]}  IsHeap(h) ∧ o ≠ null ∧ (o ≠ $BeingConstructed ∨ h[$BeingConstructed, $NonNullFieldsAreInitialized] == true) ⇒  h[o,AsNonNullRefField(f,T)] ≠ null);
axiom (∀ h: HeapType, o: ref, f: Field int, T: TName • {h[o,AsRangeField(f,T)]}  IsHeap(h)  ⇒  InRange(h[o,AsRangeField(f,T)], T));

// abstract classes, interfaces, ...
function $IsMemberlessType(TName) returns (bool);
axiom (∀ o: ref • { $IsMemberlessType($typeof(o)) }  ¬$IsMemberlessType($typeof(o)));
function $AsInterface(TName) returns (TName);

axiom (∀ J: TName • { System.Object <: $AsInterface(J) }  $AsInterface(J) == J  ⇒  ¬(System.Object <: J));

// this axiom relates a boxed struct to any interfaces that the struct implements
// otherwise, all that is known is that a boxed struct is of type System.Object which isn't strong enough
axiom (∀<T> $J: TName, s: T, b: ref • { UnboxedType(Box(s,b)) <: $AsInterface($J) } $AsInterface($J) == $J && Box(s,b)==b && UnboxedType(Box(s,b)) <: $AsInterface($J) ==> $typeof(b) <: $J);

function $HeapSucc(oldHeap: HeapType, newHeap: HeapType) returns (bool);

//------------ Immutable types

function $IsImmutable(T:TName) returns (bool);

// We say here that System.Object is mutable, but only using the $IsImmutable predicate.  The functions
// $AsImmutable and $AsMutable below are used to say that all subtypes below fixpoints of these functions
// are also fixpoints.
axiom !$IsImmutable(System.Object);

function $AsImmutable(T:TName) returns (theType: TName);
function $AsMutable(T:TName) returns (theType: TName);

axiom (∀ T: TName, U:TName • {U <: $AsImmutable(T)} U <: $AsImmutable(T) ⇒  $IsImmutable(U) ∧ $AsImmutable(U) == U);
axiom (∀ T: TName, U:TName • {U <: $AsMutable(T)} U <: $AsMutable(T) ⇒  !$IsImmutable(U) ∧ $AsMutable(U) == U);

function AsOwner(string: ref, owner: ref) returns (theString: ref);

#if ExperimentalObjectModel
axiom (∀ o: ref , T:TName • {$typeof(o) <: $AsImmutable(T)}
    o ≠ null ∧ o ≠ $BeingConstructed  ∧ $typeof(o) <: $AsImmutable(T)
    ⇒ 
    (∀ h: HeapType • {IsHeap(h)}
        IsHeap(h)
        ⇒
        (∀ S:TName • h[o,$validfor][S]) ∧
        h[o, $inv] == $typeof(o) ∧
        h[o, $ownerFrame] == $PeerGroupPlaceholder ∧
        AsOwner(o, h[o, $ownerRef]) == o ∧
        (∀ t: ref •  {AsOwner(o, h[t, $ownerRef])}
            AsOwner(o, h[t, $ownerRef]) == o
            ⇒
            t == o  ∨  h[t, $ownerFrame] ≠ $PeerGroupPlaceholder)));
#elsif !TrivialObjectModel
axiom (∀ o: ref , T:TName • {$typeof(o) <: $AsImmutable(T)}
    o ≠ null ∧ o ≠ $BeingConstructed  ∧ $typeof(o) <: $AsImmutable(T)
    ⇒ 
    (∀ h: HeapType • {IsHeap(h)}
        IsHeap(h)
        ⇒
        h[o, $inv] == $typeof(o) ∧ h[o, $localinv] == $typeof(o) ∧
        h[o, $ownerFrame] == $PeerGroupPlaceholder ∧
        AsOwner(o, h[o, $ownerRef]) == o ∧
        (∀ t: ref •  {AsOwner(o, h[t, $ownerRef])}
            AsOwner(o, h[t, $ownerRef]) == o
            ⇒
            t == o  ∨  h[t, $ownerFrame] ≠ $PeerGroupPlaceholder)));
#endif

//------------ Encode methodology

const unique System.String: TName;

function $StringLength (ref) returns (int);
axiom (∀ s:ref • {$StringLength(s)} 0 ≤ $StringLength(s));

// for rep fields
function AsRepField(f: Field ref, declaringType: TName) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (∀ h: HeapType, o: ref, f: Field ref, T: TName  •  {h[o,AsRepField(f,T)]}  IsHeap(h) ∧ h[o,AsRepField(f,T)] ≠ null  ⇒  h[h[o,AsRepField(f,T)], $ownerRef] == o ∧ h[h[o,AsRepField(f,T)], $ownerFrame] == T);
#endif

// for peer fields
function AsPeerField(f: Field ref) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (∀ h: HeapType, o: ref, f: Field ref  •  {h[o,AsPeerField(f)]}  IsHeap(h) ∧ h[o,AsPeerField(f)] ≠ null  ⇒  h[h[o,AsPeerField(f)], $ownerRef] == h[o, $ownerRef] ∧ h[h[o,AsPeerField(f)], $ownerFrame] == h[o, $ownerFrame]);
#endif

// for ElementsRep fields
function AsElementsRepField(f: Field ref, declaringType: TName, position: int) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (∀ h: HeapType, o: ref, f: Field ref, T: TName, i: int  •  {h[o,AsElementsRepField(f,T,i)]}  IsHeap(h) ∧ h[o,AsElementsRepField(f,T,i)] ≠ null  ⇒  h[$ElementProxy(h[o,AsElementsRepField(f,T,i)],i), $ownerRef] == o ∧ h[$ElementProxy(h[o,AsElementsRepField(f,T,i)],i), $ownerFrame] == T);
#endif

// for ElementsPeer fields
function AsElementsPeerField(f: Field ref, position: int) returns (theField: Field ref);

#if !TrivialObjectModel
axiom (∀ h: HeapType, o: ref, f: Field ref, i: int  •  {h[o,AsElementsPeerField(f,i)]}  IsHeap(h) ∧ h[o,AsElementsPeerField(f,i)] ≠ null  ⇒  h[$ElementProxy(h[o,AsElementsPeerField(f,i)],i), $ownerRef] == h[o, $ownerRef] ∧ h[$ElementProxy(h[o,AsElementsPeerField(f,i)],i), $ownerFrame] == h[o, $ownerFrame]);
#endif



// committed fields are fully valid
#if ExperimentalObjectModel
// this mimics what was here before, alternative: move the inner quantification
// to the outer quantification and add "h[o,$validfor][T]" to the trigger as a multi-trigger
// the above is actually the current encoding!

//axiom (∀ h:HeapType, o:ref •  {h[h[o,$ownerRef], $validfor][h[o, $ownerFrame]] }  
//   IsHeap(h) ∧ h[o,$ownerFrame] ≠ $PeerGroupPlaceholder ∧ h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ⇒  
//   (∀ T:TName • {h[o,$validfor][T]} h[o,$validfor][T]) ∧ h[o, $inv] == $typeof(o));

//axiom (forall h:HeapType, o:ref ::  {h[h[o,$ownerRef], $validfor][h[o, $ownerFrame]]} {h[o,$validfor]}  
//   IsHeap(h) && h[o,$ownerFrame] != $PeerGroupPlaceholder && h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
//   (forall T:TName :: {h[o,$validfor][T]} h[o,$validfor][T]) && h[o, $inv] == $typeof(o));

axiom (∀ h:HeapType, o:ref, T:TName • {h[o,$validfor][T]}   
  IsHeap(h) ∧ h[o,$ownerFrame] ≠ $PeerGroupPlaceholder ∧ h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
   h[o,$validfor][T] ∧ h[o, $inv] == $typeof(o));

// This axiom might help with some triggering of the one above, further tests needed!
axiom (∀ h:HeapType, o:ref • {h[o, $inv]}   
  IsHeap(h) ∧ h[o,$ownerFrame] ≠ $PeerGroupPlaceholder ∧ h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] ==>  
   h[o, $inv] == $typeof(o));

#elsif !TrivialObjectModel
axiom (∀ h:HeapType, o:ref  •  {h[h[o,$ownerRef], $inv] <: h[o, $ownerFrame] }  
   IsHeap(h) ∧ h[o,$ownerFrame] ≠ $PeerGroupPlaceholder ∧ h[h[o,$ownerRef], $inv] <: h[o, $ownerFrame]  ∧ h[h[o,$ownerRef], $localinv] ≠ $BaseClass(h[o, $ownerFrame])  ⇒  
   h[o,$inv] == $typeof(o) ∧ h[o,$localinv] == $typeof(o));

#endif

// The following procedure sets the owner of o and all its peers to (ow,fr).
// It expects o != null && o.$ownerFrame==$PeerGroupPlaceholder, but this condition is checked at the call site.
procedure $SetOwner(o: ref, ow: ref, fr: TName);
#if !TrivialObjectModel
  modifies $Heap;
  ensures (∀<alpha> p: ref, F: Field alpha •
      { $Heap[p, F] }
      (F ≠ $ownerRef ∧ F ≠ $ownerFrame) ∨
      old($Heap[p, $ownerRef] ≠ $Heap[o, $ownerRef]) ∨
      old($Heap[p, $ownerFrame] ≠ $Heap[o, $ownerFrame])
      ⇒  old($Heap[p, F]) == $Heap[p, F]);
  ensures (∀ p: ref  •
      { $Heap[p, $ownerRef] }
      { $Heap[p, $ownerFrame] }
      old($Heap[p, $ownerRef] == $Heap[o, $ownerRef]) ∧
      old($Heap[p, $ownerFrame] == $Heap[o, $ownerFrame])
      ⇒  $Heap[p, $ownerRef] == ow  ∧  $Heap[p, $ownerFrame] == fr);
  free ensures $HeapSucc(old($Heap), $Heap);
#endif

// The following procedure is called for "o.f = e;" where f is a rep field declared in a class T:
procedure $UpdateOwnersForRep(o: ref, T: TName, e: ref);
#if !TrivialObjectModel
  modifies $Heap;
  ensures (∀<alpha> p: ref, F: Field alpha  •
      { $Heap[p, F] }
      (F ≠ $ownerRef ∧ F ≠ $ownerFrame) ∨
      old($Heap[p, $ownerRef] ≠ $Heap[e, $ownerRef]) ∨
      old($Heap[p, $ownerFrame] ≠ $Heap[e, $ownerFrame])
      ⇒  old($Heap[p, F]) == $Heap[p, F]);
  ensures e == null  ⇒  $Heap == old($Heap);
  ensures e ≠ null  ⇒  (∀ p: ref  •
      { $Heap[p, $ownerRef] }
      { $Heap[p, $ownerFrame] }
      old($Heap[p, $ownerRef] == $Heap[e, $ownerRef]) ∧
      old($Heap[p, $ownerFrame] == $Heap[e, $ownerFrame])
      ⇒  $Heap[p, $ownerRef] == o  ∧  $Heap[p, $ownerFrame] == T);
  free ensures $HeapSucc(old($Heap), $Heap);
#endif

// The following procedure is called for "c.f = d;" where f is a peer field:
procedure $UpdateOwnersForPeer(c: ref, d: ref);
#if !TrivialObjectModel
  modifies $Heap;
  ensures (∀<alpha> p: ref, F: Field alpha  •
      { $Heap[p, F] }
      (F ≠ $ownerRef ∧ F ≠ $ownerFrame) ∨
      old($Heap[p, $ownerRef] ≠ $Heap[d, $ownerRef] ∨ $Heap[p, $ownerFrame] ≠ $Heap[d, $ownerFrame])
      ⇒  old($Heap[p, F]) == $Heap[p, F]);
  ensures d == null  ⇒  $Heap == old($Heap);
  ensures d ≠ null  ⇒  (∀ p: ref  •
      { $Heap[p, $ownerRef] }
      { $Heap[p, $ownerFrame] }
      old($Heap[p, $ownerRef] == $Heap[d, $ownerRef] ∧ $Heap[p, $ownerFrame] == $Heap[d, $ownerFrame])
      ⇒
      $Heap[p, $ownerRef] == old($Heap)[c, $ownerRef] ∧
      $Heap[p, $ownerFrame] == old($Heap)[c, $ownerFrame]);
  free ensures $HeapSucc(old($Heap), $Heap);
#endif


#if !TrivialObjectModel
// Intuitively, the $FirstConsistentOwner field of an object is defined as the closest
// transitive owner that is consistent.  The field is defined if the object is committed.

const unique $FirstConsistentOwner: Field ref;
#endif

function $AsPureObject(ref) returns (ref);  // used only for triggering
function ##FieldDependsOnFCO<alpha>(o: ref, f: Field alpha, ev: exposeVersionType) returns (exposeVersionType);

#if !TrivialObjectModel
// The following axiom say that for any committed object o, each field of o is determined
// by the exposeVersion of o's first consistent owner.

#if FCOAxiom_None
#elsif FCOAxiom_ExposeVersion_Only
axiom (∀ o: ref, h: HeapType  •
  { h[$AsPureObject(o), $exposeVersion] }
  IsHeap(h) ∧
  o ≠ null ∧ h[o, $allocated] == true ∧ $AsPureObject(o) == o ∧
  h[o, $ownerFrame] ≠ $PeerGroupPlaceholder ∧ 
#if ExperimentalObjectModel
  h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] 
#else
  h[h[o, $ownerRef], $inv] <: h[o, $ownerFrame] ∧
  h[h[o, $ownerRef], $localinv] ≠ $BaseClass(h[o, $ownerFrame])
#endif
  ⇒
  h[o, $exposeVersion] == ##FieldDependsOnFCO(o, $exposeVersion, h[h[o, $FirstConsistentOwner], $exposeVersion]));
#else
axiom (∀<alpha> o: ref, f: Field alpha, h: HeapType  •
  { h[$AsPureObject(o), f] }
  IsHeap(h) ∧
  o ≠ null ∧ h[o, $allocated] == true ∧ $AsPureObject(o) == o ∧
  h[o, $ownerFrame] ≠ $PeerGroupPlaceholder ∧ 
#if ExperimentalObjectModel
  h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] 
#else
  h[h[o, $ownerRef], $inv] <: h[o, $ownerFrame] ∧
  h[h[o, $ownerRef], $localinv] ≠ $BaseClass(h[o, $ownerFrame])
#endif
  ⇒
  h[o, f] == ##FieldDependsOnFCO(o, f, h[h[o, $FirstConsistentOwner], $exposeVersion]));
#endif

axiom (∀ o: ref, h: HeapType  •
  { h[o, $FirstConsistentOwner] }
  IsHeap(h) ∧
  o ≠ null ∧ h[o, $allocated] == true ∧
  h[o, $ownerFrame] ≠ $PeerGroupPlaceholder ∧ 
#if ExperimentalObjectModel
  h[h[o,$ownerRef],$validfor][h[o, $ownerFrame]] 
#else
  h[h[o, $ownerRef], $inv] <: h[o, $ownerFrame] ∧
  h[h[o, $ownerRef], $localinv] ≠ $BaseClass(h[o, $ownerFrame])
#endif
  ⇒
  h[o, $FirstConsistentOwner] != null ∧
  h[h[o, $FirstConsistentOwner], $allocated] == true ∧
  // ¬ h[h[o, $FirstConsistentOwner], Committed]
  (h[h[o, $FirstConsistentOwner], $ownerFrame] == $PeerGroupPlaceholder ∨
#if ExperimentalObjectModel
   ¬(h[h[h[o, $FirstConsistentOwner],$ownerRef],$validfor][h[h[o, $FirstConsistentOwner], $ownerFrame]]) ));
#else
   ¬(h[h[h[o, $FirstConsistentOwner], $ownerRef], $inv] <: h[h[o, $FirstConsistentOwner], $ownerFrame]) ∨
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

axiom (∀<T> value: T, typ: TName, occurrence: NondetType, activity: ActivityType •
  { NewInstance(BoxFunc(value, typ), occurrence, activity) }
  Box(value, NewInstance(BoxFunc(value, typ), occurrence, activity)) == NewInstance(BoxFunc(value, typ), occurrence, activity) ∧
  UnboxedType(NewInstance(BoxFunc(value, typ), occurrence, activity)) == typ);

// Sometimes boxing is just the identity function: namely when its argument is a reference type 
axiom (∀ x:ref, typ : TName, occurrence: NondetType, activity : ActivityType • 
                  ¬$IsValueType(UnboxedType(x))
              ⇒ NewInstance(BoxFunc(x,typ), occurrence,activity) == x);

// For simplicity, we track boxed values stored to locals, not those stored into the heap.
axiom (∀<T> x: T, p: ref •  {Unbox(Box(x,p)): T}  Unbox(Box(x,p)) == x);

function UnboxedType(ref) returns (TName);

// Boxes are always consistent
#if ExperimentalObjectModel
axiom (∀ p: ref •  {$IsValueType(UnboxedType(p))}  $IsValueType(UnboxedType(p))  ⇒
  (∀<T> heap: HeapType, x: T •  {heap[Box(x,p),$inv]} {heap[Box(x,p),$validfor]}  IsHeap(heap)  ⇒
    heap[Box(x,p),$inv] == $typeof(Box(x,p)) ∧ (∀ T: TName • {heap[Box(x,p),$validfor][T]} heap[Box(x,p),$validfor][T])));
#elsif !TrivialObjectModel
axiom (∀ p: ref •  {$IsValueType(UnboxedType(p))}  $IsValueType(UnboxedType(p))  ⇒
  (∀<T> heap: HeapType, x: T •  {heap[Box(x,p),$inv]}  IsHeap(heap)  ⇒
    heap[Box(x,p),$inv] == $typeof(Box(x,p)) ∧ heap[Box(x,p),$localinv] == $typeof(Box(x,p))));
#endif

// For reference types, boxing returns the reference
axiom (∀<T> x:T, p:ref •  {UnboxedType(Box(x,p)) <: System.Object}  UnboxedType(Box(x,p)) <: System.Object ∧ Box(x,p) == p  ⇒  x == p);

// BoxTester is the value type equivalent of $As
function BoxTester(p:ref, typ: TName) returns (ref);
axiom (∀ p:ref, typ: TName •  {BoxTester(p,typ)}  UnboxedType(p) == typ  ⇔  BoxTester(p,typ) ≠ null);
axiom (∀ p:ref, typ: TName •  {BoxTester(p,typ)}  BoxTester(p,typ) ≠ null  ⇒ (∀<T> •  Box(Unbox(p): T, p) == p));

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
axiom (∀<U> x: ref, p: ref • { Unbox(Box(x, p)):U }  Box(x,p) == p  ⇒
  Unbox(Box(x, p)):U == UnBoxDisguise(x) ∧
  BoxDisguise(Unbox(Box(x, p)):U) == x);


axiom (∀ typ: TName, occurrence: NondetType, activity: ActivityType •
  { NewInstance(AllocFunc(typ), occurrence, activity) }
  $typeof(NewInstance(AllocFunc(typ), occurrence, activity)) == typ ∧
  NewInstance(AllocFunc(typ), occurrence, activity) != null);

axiom (∀ typ: TName, occurrence: NondetType, activity: ActivityType, heap: HeapType •
  {heap[NewInstance(AllocFunc(typ), occurrence, activity),$allocated]}  IsHeap(heap)  ⇒
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
axiom (∀ i:int • InRange(i, System.SByte)  ⇔  -128 ≤ i ∧ i < 128);
axiom (∀ i:int • InRange(i, System.Byte)  ⇔  0 ≤ i ∧ i < 256);
axiom (∀ i:int • InRange(i, System.Int16)  ⇔  -32768 ≤ i ∧ i < 32768);
axiom (∀ i:int • InRange(i, System.UInt16)  ⇔  0 ≤ i ∧ i < 65536);
axiom (∀ i:int • InRange(i, System.Int32)  ⇔  -2147483648 ≤ i ∧ i ≤ 2147483647);
axiom (∀ i:int • InRange(i, System.UInt32)  ⇔  0 ≤ i ∧ i ≤ 4294967295);
axiom (∀ i:int • InRange(i, System.Int64)  ⇔  -9223372036854775808 ≤ i ∧ i ≤ 9223372036854775807);
axiom (∀ i:int • InRange(i, System.UInt64)  ⇔  0 ≤ i ∧ i ≤ 18446744073709551615);
axiom (∀ i:int • InRange(i, System.Char)  ⇔  0 ≤ i ∧ i < 65536);


//---------- Type conversions and sizes


function $IntToInt(val: int, fromType: TName, toType: TName) returns (int);
function $IntToReal(int, fromType: TName, toType: TName) returns (real);
function $RealToInt(real, fromType: TName, toType: TName) returns (int);
function $RealToReal(val: real, fromType: TName, toType: TName) returns (real);

axiom (∀ z: int, B: TName, C: TName • InRange(z, C) ⇒ $IntToInt(z, B, C) == z);

function $SizeIs (TName, int) returns (bool); // SizeIs(T,n) means that n = sizeof(T)



//------------ Formula/term operators

function $IfThenElse<a>(bool, a, a) returns (a);

axiom (∀<a> b:bool, x:a, y:a • {$IfThenElse(b,x,y)} b ⇒  $IfThenElse(b,x,y) == x);
axiom (∀<a> b:bool, x:a, y:a • {$IfThenElse(b,x,y)} ¬b ⇒  $IfThenElse(b,x,y) == y);

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
axiom (∀ x:int, y:int • {x % y} {x / y}  x % y == x - x / y * y);

// remainder is C# is complicated, because division rounds toward 0
axiom (∀ x:int, y:int • {x % y}  0 ≤ x ∧ 0 < y  ⇒  0 ≤ x % y  ∧  x % y < y);
axiom (∀ x:int, y:int • {x % y}  0 ≤ x ∧ y < 0  ⇒  0 ≤ x % y  ∧  x % y < -y);
axiom (∀ x:int, y:int • {x % y}  x ≤ 0 ∧ 0 < y  ⇒  -y < x % y  ∧  x % y ≤ 0);
axiom (∀ x:int, y:int • {x % y}  x ≤ 0 ∧ y < 0  ⇒  y < x % y  ∧  x % y ≤ 0);

axiom (∀ x:int, y:int • {(x + y) % y}  0 ≤ x ∧ 0 ≤ y  ⇒  (x + y) % y == x % y);
// do we need this symmetric one, too?
axiom (∀ x:int, y:int • {(y + x) % y}  0 ≤ x ∧ 0 ≤ y  ⇒  (y + x) % y == x % y);
axiom (∀ x:int, y:int • {(x - y) % y}  0 ≤ x-y ∧ 0 ≤ y  ⇒  (x - y) % y == x % y);

// the following axiom prevents a matching loop in Simplify
// axiom (∀ x:int, y:int • {x * y / y * y}  x * y / y * y == x * y);

// the following axiom has some unfortunate matching, but it does state a property about % that
// is sometime useful
axiom (∀ a: int, b: int, d: int • { a % d, b % d } 2 ≤ d ∧ a % d == b % d ∧ a < b  ⇒  a + d ≤ b);

#if ArithDistributionAxioms
//  These axioms provide good functionality, but in some cases they can be very expensive
// distributions of * and +/-
axiom (∀ x: int, y: int, z: int •  { (x+y)*z }  (x+y)*z == x*z + y*z);
axiom (∀ x: int, y: int, z: int •  { (x-y)*z }  (x-y)*z == x*z - y*z);
axiom (∀ x: int, y: int, z: int •  { z*(x+y) }  z*(x+y) == z*x + z*y);
axiom (∀ x: int, y: int, z: int •  { z*(x-y) }  z*(x-y) == z*x - z*y);
#endif

axiom (∀ x: int, y: int • { #and(x,y) }  0 ≤ x ∨ 0 ≤ y  ⇒  0 ≤ #and(x,y));
axiom (∀ x: int, y: int • { #or(x,y) }  0 ≤ x ∧ 0 ≤ y  ⇒  0 ≤ #or(x,y) ∧ #or(x,y) ≤ x + y);

axiom (∀ i:int • {#shl(i,0)} #shl(i,0) == i);
axiom (∀ i:int, j:int • {#shl(i,j)}  1 ≤ j ⇒ #shl(i,j) == #shl(i,j-1) * 2);
axiom (∀ i:int, j:int • {#shl(i,j)} 0 ≤ i ∧ i < 32768 ∧ 0 ≤ j ∧ j ≤ 16  ⇒  0 ≤ #shl(i, j) ∧ #shl(i, j) ≤ 2147483647);

axiom (∀ i:int • {#shr(i,0)} #shr(i,0) == i);
axiom (∀ i:int, j:int • {#shr(i,j)} 1 ≤ j ⇒ #shr(i,j) == #shr(i,j-1) / 2);


function #min(int, int) returns (int);
function #max(int, int) returns (int);
axiom (∀ x: int, y: int • { #min(x,y) } (#min(x,y) == x ∨ #min(x,y) == y) ∧ #min(x,y) ≤ x ∧ #min(x,y) ≤ y);
axiom (∀ x: int, y: int • { #max(x,y) } (#max(x,y) == x ∨ #max(x,y) == y) ∧ x ≤ #max(x,y) ∧ y ≤ #max(x,y));


//---------- Properties of String (Literals)

function #System.String.IsInterned$System.String$notnull(HeapType, ref) returns (ref);
function #System.String.Equals$System.String(HeapType, ref, ref) returns (bool);
function #System.String.Equals$System.String$System.String(HeapType, ref, ref) returns (bool);
function ##StringEquals(ref, ref) returns (bool);

// two names for String.Equals
axiom (∀ h: HeapType, a: ref, b: ref •
 { #System.String.Equals$System.String(h, a, b) }
 #System.String.Equals$System.String(h, a, b) == #System.String.Equals$System.String$System.String(h, a, b));

// String.Equals is independent of the heap, and it is reflexive and commutative
axiom (∀ h: HeapType, a: ref, b: ref •
 { #System.String.Equals$System.String$System.String(h, a, b) }
 #System.String.Equals$System.String$System.String(h, a, b) == ##StringEquals(a, b) ∧
 #System.String.Equals$System.String$System.String(h, a, b) == ##StringEquals(b, a) ∧
 (a == b  ⇒  ##StringEquals(a, b)));

// String.Equals is also transitive
axiom (∀ a: ref, b: ref, c: ref •  ##StringEquals(a, b) ∧ ##StringEquals(b, c)  ⇒  ##StringEquals(a, c));

// equal strings have the same interned ref
axiom (∀ h: HeapType, a: ref, b: ref •
 { #System.String.Equals$System.String$System.String(h, a, b) }
 a ≠ null ∧ b ≠ null ∧ #System.String.Equals$System.String$System.String(h, a, b)
 ⇒
 #System.String.IsInterned$System.String$notnull(h, a) == 
 #System.String.IsInterned$System.String$notnull(h, b));

// ************** END PRELUDE **************
