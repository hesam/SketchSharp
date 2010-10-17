//-----------------------------------------------------------------------------
//
// Copyright (C) Microsoft Corporation.  All Rights Reserved.
//
//-----------------------------------------------------------------------------
// This file needs to stay in sync with Contracts.cs in the Foxtrot project.
// The Spec# compiler uses this version instead of the Foxtrot version even if
// the code being compiled references the Foxtrot version.
// This is needed because of name clashes (PureAttribute, etc.)
#define DEBUG // The behavior of this contract library should be consistent regardless of build type.
#define FEATURE_SERIALIZATION
#define USE_DEFAULT_TRACE_LISTENER

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
#if FEATURE_SERIALIZATION
using System.Runtime.Serialization;
#endif
using System.Collections.Generic;
#if !MIDORI
using System.Security.Permissions;
#endif

namespace Microsoft.Contracts {
  #region Attributes

  /// <summary>
  /// Methods and classes marked with this attribute can be used within calls to Contract methods. Such methods not make any visible state changes.
  /// </summary>
  [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Event | AttributeTargets.Delegate | AttributeTargets.Class | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
  public sealed class PureAttribute : Attribute {
    public enum PurityLevel { Normal = 1, Assumed = 2, Strong = 4, Weak = 8, Observational = 16 };
    public bool Value;
    private PurityLevel purityLevel;

    public PureAttribute () { this.Value = true; purityLevel = PurityLevel.Normal | PurityLevel.Weak; }
    public PureAttribute (bool value) { this.Value = value; purityLevel = PurityLevel.Normal | PurityLevel.Weak; }
    public PureAttribute (PurityLevel pl) { this.Value = true; purityLevel = pl | PurityLevel.Weak; }
    public bool IsAssumedPure { get { return (this.purityLevel & PurityLevel.Assumed) != 0; } }
    public bool IsWeaklyPure { get { return (this.purityLevel & PurityLevel.Assumed) != 0; } }
  }

  /// <summary>
  /// Types marked with this attribute specify that a separate type contains the contracts for this type.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
  public sealed class ContractClassAttribute : Attribute {
    private Type _typeWithContracts;

    public ContractClassAttribute (Type typeContainingContracts) {
      _typeWithContracts = typeContainingContracts;
    }

    public Type TypeContainingContracts {
      get { return _typeWithContracts; }
    }
  }

  /// <summary>
  /// Types marked with this attribute specify that they are a contract for the type that is the argument of the constructor.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
  public sealed class ContractClassForAttribute : Attribute {
    private Type _typeIAmAContractFor;

    public ContractClassForAttribute (Type typeContractsAreFor) {
      _typeIAmAContractFor = typeContractsAreFor;
    }

    public Type TypeContractsAreFor {
      get { return _typeIAmAContractFor; }
    }
  }

  /// <summary>
  /// This attribute is used to mark a method as being the invariant
  /// method for a class. The method can have any name, but it must
  /// return "void" and take no parameters. The body of the method
  /// must consist solely of one or more calls to the method
  /// Contract.Invariant.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
  public sealed class InvariantMethodAttribute : Attribute {
  }

  /// <summary>
  /// Attribute that specifies that an assembly has runtime contract checks.
  /// </summary>
  [AttributeUsage(AttributeTargets.Assembly)]
  public sealed class RuntimeContractsAttribute : Attribute {
  }

#if FEATURE_SERIALIZATION
  [Serializable]
#endif
  public enum Mutability {
    Unspecified,
    Immutable,    // read-only after construction, except for lazy initialization & caches
    // Do we need a "deeply immutable" value?
    Mutable,
    HasInitializationPhase,  // read-only after some point.  
    // Do we need a value for mutable types with read-only wrapper subclasses?
  }
  // Note: This hasn't been thought through in any depth yet.  Consider it experimental.
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
  [SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments", Justification = "Thank you very much, but we like the names we've defined for the accessors")]
  public sealed class MutabilityAttribute : Attribute {
    private Mutability _mutabilityMarker;

    public MutabilityAttribute (Mutability mutabilityMarker) {
      _mutabilityMarker = mutabilityMarker;
    }

    public Mutability Mutability {
      get { return _mutabilityMarker; }
    }
  }

  /// <summary>
  /// Instructs downstream tools whether to assume the correctness of this assembly, type or member without performing any verification or not.
  /// Can use [Verify(false)] to explicitly mark assembly, type or member as one to *not* have verification performed on it.
  /// Most specific element found (member, type, then assembly) takes precedence.
  /// (That is useful if downstream tools allow a user to decide which polarity is the default, unmarked case.)
  /// </summary>
  /// <remarks>
  /// Apply this attribute to a type to apply to all members of the type, including nested types.
  /// Apply this attribute to an assembly to apply to all types and members of the assembly.
  /// Apply this attribute to a property to apply to both the getter and setter.
  /// Default is true, so [Verify] is the same as [Verify(true)].
  /// </remarks>
  [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property)]
  public sealed class VerifyAttribute : Attribute {
    private bool _value;

    public VerifyAttribute () { _value = true; }

    public VerifyAttribute (bool value) { _value = value; }

    public bool Value {
      get { return _value; }
    }
  }

  /// <summary>
  /// Allows a field f to be used in the method contracts for a method m when f has less visibility than m.
  /// For instance, if the method is public, but the field is private.
  /// </summary>
  [Conditional("FEATURE_FULL_CONTRACTS")]
  [AttributeUsage(AttributeTargets.Field)]
  [SuppressMessage("Microsoft.Design", "CA1019:DefineAccessorsForAttributeArguments", Justification = "Thank you very much, but we like the names we've defined for the accessors")]
  public sealed class SpecPublicAttribute : Attribute {
    private string _publicName;

    public SpecPublicAttribute () { }

    public SpecPublicAttribute (string publicName) {
      _publicName = publicName;
    }

    public string Name {
      get { return _publicName; }
    }
  }

  #endregion Attributes

  /// <summary>
  /// Contains static methods for representing program contracts such as preconditions, postconditions, and invariants.
  /// </summary>
  /// <remarks>
  /// WARNING: A binary rewriter must be used to insert runtime enforcement of these contracts.
  /// Otherwise some contracts like Ensures can only be checked statically and will not throw exceptions during runtime when contracts are violated.
  /// </remarks>
  public static class Contract {

    #region Private Methods
    private static void AssertImpl (bool condition, string message) {
      // @TODO: MDA.  
      // @TODO: Consider converting expression to a String, then passing that as the second parameter.
      //System.Diagnostics.Assert.Check(condition, message, message);
      Debug.Assert(condition, message);
    }
    #endregion

    #region User Methods

    #region Assume

    /// <summary>
    /// Instructs code analysis tools to assume the expression <paramref name="condition"/> is true even if it can not be statically proven to always be true.
    /// </summary>
    /// <param name="condition">Expression to assume will always be true.</param>
    /// <remarks>
    /// At runtime this is equivalent to an <seealso cref="Microsoft.Contracts.Contract.Assert(bool)"/>.
    /// </remarks>
    [Pure]
    [Conditional("DEBUG")]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    public static void Assume (bool condition) {
      AssertImpl(condition, "Assumption failed");
      if (!condition) throw new AssumptionException("Assumption failed");
    }

    #endregion Assume

    #region Assert

    /// <summary>
    /// In debug builds, perform a runtime check that <paramref name="condition"/> is true.
    /// </summary>
    /// <param name="condition">Expression to check to always be true.</param>
    [Pure]
    [Conditional("DEBUG")]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    public static void Assert (bool condition) {
      AssertImpl(condition, "Assertion failed");
      if (!condition) throw new AssertionException("Assertion failed");
    }

    #endregion Assert

    #region Requires

    /// <summary>
    /// Specifies a contract such that the expression <paramref name="condition"/> must be true before the enclosing method or property is invoked.
    /// </summary>
    /// <param name="condition">Boolean expression representing the contract.</param>
    /// <remarks>
    /// This call must happen at the beginning of a method or property before any other code.
    /// This contract is exposed to clients so must only reference members at least as visible as the enclosing method.
    /// Use this form when backward compatibility does not force you to throw a particular exception.
    /// </remarks>
    [Pure]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    [Conditional("FEATURE_RUNTIME_PRECONDITIONS")]
    public static void Requires (bool condition) {

      if (!condition) {
        AssertImpl(condition, "Precondition failed");  // For debugging, in the absence of an MDA
        throw new PreconditionException();
      }
    }

    /// <summary>
    /// Specifies a contract such that the expression <paramref name="condition"/> must be true before the enclosing method or property is invoked.
    /// </summary>
    /// <param name="condition">Boolean expression representing the contract.</param>
    /// <remarks>
    /// This call must happen at the beginning of a method or property before any other code.
    /// This contract is exposed to clients so must only reference members at least as visible as the enclosing method.
    /// Use this form when you want a check in the retail build and backward compatibility does not
    /// force you to throw a particular exception.
    /// </remarks>
    [Pure]
    public static void RequiresInRetail (bool condition) {
      if (!condition) {
        AssertImpl(condition, "Precondition failed");  // For debugging, in the absence of an MDA
        throw new PreconditionException();
      }
    }

    #endregion Requires

    #region Ensures

    /// <summary>
    /// Specifies a public contract such that the expression <paramref name="condition"/> will be true when the enclosing method or property returns normally.
    /// </summary>
    /// <param name="condition">Boolean expression representing the contract.  May include <seealso cref="Old"/> and <seealso cref="Result"/>.</param>
    /// <remarks>
    /// This call must happen at the beginning of a method or property before any other code.
    /// This contract is exposed to clients so must only reference members at least as visible as the enclosing method.
    /// The contract rewriter must be used for runtime enforcement of this postcondition.
    /// </remarks>
    [Pure]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.String.Concat(System.String,System.String)", Justification = "Not intended to be called at runtime.")]
    public static void Ensures (bool condition) {
      String.Concat(
          "This method will be modified to the following after rewriting:",
          "if (!condition) throw new PostConditionException();");
    }

    /// <summary>
    /// Specifies a contract such that an exception of type <typeparamref name="TException"/> may be thrown.
    /// </summary>
    /// <typeparam name="TException">Type of exception that may be thrown.</typeparam>
    /// <remarks>
    /// This call must happen at the beginning of a method or property before any other code.
    /// This contract is exposed to clients so must only reference types at least as visible as the enclosing method.
    /// </remarks>
    [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Not intended to be called at runtime.")]
    [Pure]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    public static void Throws<TException> () where TException : Exception {
    }

    /// <summary>
    /// Specifies a contract such that if an exception of type <typeparamref name="TException"/> is thrown then the expression <paramref name="condition"/> will be true when the enclosing method or property terminates abnormally.
    /// </summary>
    /// <typeparam name="TException">Type of exception related to this postcondition.</typeparam>
    /// <param name="condition">Boolean expression representing the contract.  May include <seealso cref="Old"/> and <seealso cref="Result"/>.</param>
    /// <remarks>
    /// This call must happen at the beginning of a method or property before any other code.
    /// This contract is exposed to clients so must only reference types and members at least as visible as the enclosing method.
    /// The contract rewriter must be used for runtime enforcement of this postcondition.
    /// </remarks>
    [Conditional("FEATURE_FULL_CONTRACTS")]
    [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.String.Concat(System.String,System.String,System.String,System.String)", Justification = "Not intended to be called at runtime.")]
    [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Not intended to be called at runtime.")]
    [Pure]
    public static void EnsuresOnThrow<TException> (bool condition) where TException : Exception {
      String.Concat(
          "This method will be modified to the following after rewriting:",
          "if (!condition) throw new PostconditionException();",
          "The rewritten code will be placed in a catch block that catches exceptions of type TException,",
          "where the body of the method is within the associated try block"
          );
    }

    /// <summary>
    /// Specifies a contract that must be true when the enclosing method or property returns, either normally or abnormally.
    /// </summary>
    /// <param name="condition">Boolean expression representing the contract.  May include <seealso cref="Old"/> and <seealso cref="Result"/>.</param>
    /// <remarks>
    /// This call must happen at the beginning of a method or property before any other code.
    /// This contract is exposed to clients so must only reference types and members at least as visible as the enclosing method.
    /// The contract rewriter must be used for runtime enforcement of this postcondition.
    /// </remarks>
    [Pure]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.String.Concat(System.String,System.String,System.String,System.String)", Justification = "Not intended to be called at runtime.")]
    public static void EnsuresFinally (bool condition) {
      String.Concat(
          "This method will be modified to the following after rewriting:",
          "if (!condition) throw new PostconditionException();",
          "The rewritten code will be placed in a finally block,",
          "where the body of the method is within the associated try block"
          );
    }

    #region Old, Result, and Out Parameters

    /// <summary>
    /// Represents the result (a.k.a. return value) of a method or property.
    /// </summary>
    /// <typeparam name="T">Type of return value of the enclosing method or property.</typeparam>
    /// <returns>Return value of the enclosing method or property.</returns>
    /// <remarks>
    /// This method can only be used within the argument to the <seealso cref="Ensures"/> contract.
    /// </remarks>
    [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter", Justification = "Not intended to be called at runtime.")]
    [Pure]
    public static T Result<T> () { return default(T); }

    /// <summary>
    /// Represents the final (output) value of an out parameter.
    /// </summary>
    /// <typeparam name="T">Type of the out parameter.</typeparam>
    /// <param name="value">The out parameter.</param>
    /// <returns>The output value of the out parameter.</returns>
    /// <remarks>
    /// This method can only be used within the argument to the <seealso cref="Ensures"/> contract.
    /// </remarks>
    [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "0#", Justification = "Not intended to be called at runtime.")]
    [Pure]
    public static T Parameter<T> (out T value) { value = default(T); return value; }

    /// <summary>
    /// Represents the value of <paramref name="value"/> as it was at the start of the method or property.
    /// </summary>
    /// <typeparam name="T">Type of <paramref name="value"/>.  This can be inferred.</typeparam>
    /// <param name="value">Value to represent.  This must be a field or parameter.</param>
    /// <returns>Value of <paramref name="value"/> at the start of the method or property.</returns>
    /// <remarks>
    /// This method can only be used within the argument to the <seealso cref="Ensures"/> contract.
    /// </remarks>
    [Pure]
    public static T Old<T> (T value) { return value; }

    #endregion Old, Result, and Out Parameters

    #endregion Ensures

    #region Invariant

    /// <summary>
    /// Specifies a contract such that the expression <paramref name="condition"/> will be true after every method or property on the enclosing class.
    /// </summary>
    /// <param name="condition">Boolean expression representing the contract.</param>
    /// <remarks>
    /// This contact can only be specified in a dedicated invariant method declared on a class.
    /// This contract is not exposed to clients so may reference members less visible as the enclosing method.
    /// The contract rewriter must be used for runtime enforcement of this invariant.
    /// </remarks>
    [Pure]
    [Conditional("FEATURE_FULL_CONTRACTS")]
    [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.String.Concat(System.String,System.String)", Justification = "Not intended to be called at runtime.")]
    public static void Invariant (bool condition) {
      String.Concat(
          "This method will be modified to the following after rewriting:",
          "if (!condition) throw new InvariantException();");
    }

    #endregion Invariant

    #region Quantifiers

    #region ForAll

    /// <summary>
    /// Returns whether the predicate function delegate <paramref name="predicate"/> returns <c>true</c> 
    /// for all integers starting from <paramref name="lo"/> to <paramref name="hi"/> - 1.
    /// </summary>
    /// <param name="inclusiveLowerBound">First integer to pass to <paramref name="predicate"/>.</param>
    /// <param name="exclusiveUpperBound">One greater than the last integer to pass to <paramref name="predicate"/>.</param>
    /// <param name="predicate">Predicate function that is evaluated from <paramref name="lo"/> to <paramref name="hi"/> - 1.</param>
    /// <returns><c>true</c> if <paramref name="predicate"/> returns <c>true</c> for all integers 
    /// starting from <paramref name="lo"/> to <paramref name="hi"/> - 1.</returns>
    /// <seealso cref="System.Collections.Generic.List.TrueForAll"/>
    public static bool ForAll (int inclusiveLowerBound, int exclusiveUpperBound, Predicate<int> predicate) {
      Contract.Requires(inclusiveLowerBound <= exclusiveUpperBound);
      Contract.Requires(predicate != null);

      for (int i = inclusiveLowerBound; i < exclusiveUpperBound; i++)
        if (!predicate(i)) return false;
      return true;
    }
    /// <summary>
    /// Returns whether the predicate function delegate <paramref name="predicate"/> returns <c>true</c> 
    /// for all elements in the <paramref name="collection"/>.
    /// </summary>
    /// <param name="collection">The collection from which elements will be drawn from to pass to <paramref name="predicate"/>.</param>
    /// <param name="predicate">Predicate function that is evaluated on elements from <paramref name="collection"/>.</param>
    /// <returns><c>true</c> if and only if <paramref name="predicate"/> returns <c>true</c> for all elements in
    /// <paramref name="collection"/>.</returns>
    /// <seealso cref="System.Collections.Generic.List.TrueForAll"/>
    /// <remarks>
    /// Once C# v3 is released, the first parameter will have the "this" marking
    /// so the method can be used as if it is an instance method on the <paramref name="collection"/>.
    /// </remarks>
    public static bool ForAll<T> (/*this*/ IEnumerable<T> collection, Predicate<T> predicate) {
      Contract.Requires(predicate != null);
      foreach (T t in collection)
        if (!predicate(t)) return false;
      return true;
    }
    #endregion ForAll

    #region Exists

    /// <summary>
    /// Returns whether the predicate function delegate <paramref name="predicate"/> returns <c>true</c> 
    /// for any integer starting from <paramref name="lo"/> to <paramref name="hi"/> - 1.
    /// </summary>
    /// <param name="inclusiveLowerBound">First integer to pass to <paramref name="predicate"/>.</param>
    /// <param name="exclusiveUpperBoundi">One greater than the last integer to pass to <paramref name="predicate"/>.</param>
    /// <param name="predicate">Predicate function that is evaluated from <paramref name="lo"/> to <paramref name="hi"/> - 1.</param>
    /// <returns><c>true</c> if <paramref name="predicate"/> returns <c>true</c> for any integer
    /// starting from <paramref name="lo"/> to <paramref name="hi"/> - 1.</returns>
    /// <seealso cref="System.Collections.Generic.List.Exists"/>
    public static bool Exists (int inclusiveLowerBound, int exclusiveUpperBound, Predicate<int> predicate) {
      Contract.Requires(inclusiveLowerBound <= exclusiveUpperBound);
      Contract.Requires(predicate != null);

      for (int i = inclusiveLowerBound; i < exclusiveUpperBound; i++)
        if (predicate(i)) return true;
      return false;
    }
    /// <summary>
    /// Returns whether the predicate function delegate <paramref name="predicate"/> returns <c>true</c> 
    /// for any element in the <paramref name="collection"/>.
    /// </summary>
    /// <param name="collection">The collection from which elements will be drawn from to pass to <paramref name="predicate"/>.</param>
    /// <param name="predicate">Predicate function that is evaluated on elements from <paramref name="collection"/>.</param>
    /// <returns><c>true</c> if and only if <paramref name="predicate"/> returns <c>true</c> for an element in
    /// <paramref name="collection"/>.</returns>
    /// <seealso cref="System.Collections.Generic.List.Exists"/>
    /// <remarks>
    /// Once C# v3 is released, the first parameter will have the "this" marking
    /// so the method can be used as if it is an instance method on the <paramref name="collection"/>.
    /// </remarks>
    public static bool Exists<T> (/*this*/ IEnumerable<T> collection, Predicate<T> predicate) {
      Contract.Requires(predicate != null);
      foreach (T t in collection)
        if (predicate(t)) return true;
      return false;
    }

    #endregion Exists

    #endregion Quantifiers

    #region Pointers

    /// <summary>
    /// Runtime checking for pointer bounds is not currently feasible. Thus, at runtime, we just return
    /// a very long extent for each pointer that is writable. As long as assertions are of the form
    /// WritableBytes(ptr) >= ..., the runtime assertions will not fail.
    /// The runtime value is 2^64 - 1 or 2^32 - 1.
    /// </summary>
    [SuppressMessage("Microsoft.Performance", "CA1802", Justification = "FxCop is confused")]
    static readonly ulong MaxWritableExtent = (UIntPtr.Size == 4) ? UInt32.MaxValue : UInt64.MaxValue;

    /// <summary>
    /// Allows specifying a writable extent for a UIntPtr, similar to SAL's writable extent.
    /// NOTE: this is for static checking only. No useful runtime code can be generated for this
    /// at the moment.
    /// </summary>
    /// <param name="startAddress">Start of memory region</param>
    /// <returns>The result is the number of bytes writable starting at <paramref name="startAddress"/></returns>
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "startAddress", Justification = "Not intended to be called at runtime.")]
    public static ulong WritableBytes (UIntPtr startAddress) { return MaxWritableExtent; }

    /// <summary>
    /// Allows specifying a writable extent for a UIntPtr, similar to SAL's writable extent.
    /// NOTE: this is for static checking only. No useful runtime code can be generated for this
    /// at the moment.
    /// </summary>
    /// <param name="startAddress">Start of memory region</param>
    /// <returns>The result is the number of bytes writable starting at <paramref name="startAddress"/></returns>
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "startAddress", Justification = "Not intended to be called at runtime.")]
    public static ulong WritableBytes (IntPtr startAddress) { return MaxWritableExtent; }

    /// <summary>
    /// Allows specifying a writable extent for a UIntPtr, similar to SAL's writable extent.
    /// NOTE: this is for static checking only. No useful runtime code can be generated for this
    /// at the moment.
    /// </summary>
    /// <param name="startAddress">Start of memory region</param>
    /// <returns>The result is the number of bytes writable starting at <paramref name="startAddress"/></returns>
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "startAddress", Justification = "Not intended to be called at runtime.")]
    unsafe public static ulong WritableBytes (void* startAddress) { return MaxWritableExtent; }

    #endregion

    #region Misc.

    /// <summary>
    /// Marker to indicate the end of the contract section of a method.
    /// </summary>
    [Conditional("FEATURE_FULL_CONTRACTS")]
    [Conditional("FEATURE_RUNTIME_PRECONDITIONS")]
    public static void EndContract () { }

    #endregion

    #endregion User Methods

    #region Rewriter Methods

    #region Requires
    [Pure]
    public static void RewriterRequires (bool condition) {
      if (!condition) {
        AssertImpl(condition, "Precondition failed");  // @TODO: MDA
        throw new PreconditionException();
      }
    }
    #endregion

    #region Ensures

    [Pure]
    public static void RewriterEnsures (bool condition) {
      if (!condition) {
        AssertImpl(condition, "Postcondition failed");  // @TODO: MDA
        throw new PostconditionException();
      }
    }

    #endregion Ensures

    #region Invariant

    [Pure]
    public static void RewriterInvariant (bool condition) {
      if (!condition) {
        AssertImpl(condition, "Invariant failed");  // @TODO: MDA
        throw new InvariantException();
      }
    }

    #endregion Invariant

    #endregion Rewriter Methods

    #region Exceptions

#if FEATURE_SERIALIZATION
    [Serializable]
#endif
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Not intended to be called at runtime.")]
    public abstract class ContractException : Exception {
      protected ContractException () : this("Contract failed.") { }
      protected ContractException (string message) : base(message) { }
      protected ContractException (string message, Exception inner) : base(message, inner) { }
#if FEATURE_SERIALIZATION
#if !MIDORI
      [SecurityPermissionAttribute(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
#endif
      protected ContractException (SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if FEATURE_SERIALIZATION
    [Serializable]
#endif
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Not intended to be called at runtime.")]
    public sealed class PreconditionException : ContractException {
      public PreconditionException () : this("Precondition failed.") { }
      public PreconditionException (string message) : base(message) { }
      public PreconditionException (string message, Exception inner) : base(message, inner) { }
#if FEATURE_SERIALIZATION
      private PreconditionException (SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if FEATURE_SERIALIZATION
    [Serializable]
#endif
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Not intended to be called at runtime.")]
    [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", Justification = "Don't tell me how to spell, damnit.")]
    public sealed class PostconditionException : ContractException {
      public PostconditionException () : this("Postcondition failed.") { }
      public PostconditionException (string message) : base(message) { }
      public PostconditionException (string message, Exception inner) : base(message, inner) { }
#if FEATURE_SERIALIZATION
      private PostconditionException (SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if FEATURE_SERIALIZATION
    [Serializable]
#endif
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Not intended to be called at runtime.")]
    public sealed class InvariantException : ContractException {
      public InvariantException () { }
      public InvariantException (string message) : base(message) { }
      public InvariantException (string message, Exception inner) : base(message, inner) { }
#if FEATURE_SERIALIZATION
      private InvariantException (SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if FEATURE_SERIALIZATION
    [Serializable]
#endif
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Not intended to be called at runtime.")]
    public sealed class AssertionException : ContractException {
      public AssertionException () { }
      public AssertionException (string message) : base(message) { }
      public AssertionException (string message, Exception inner) : base(message, inner) { }
#if FEATURE_SERIALIZATION
      private AssertionException (SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }

#if FEATURE_SERIALIZATION
    [Serializable]
#endif
    [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "Not intended to be called at runtime.")]
    public sealed class AssumptionException : ContractException {
      public AssumptionException () { }
      public AssumptionException (string message) : base(message) { }
      public AssumptionException (string message, Exception inner) : base(message, inner) { }
#if FEATURE_SERIALIZATION
      private AssumptionException (SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }


    #endregion Exceptions
  }
}