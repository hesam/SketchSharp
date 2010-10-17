using System;
using System.Compiler;

namespace Data
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false)]
    public class Attr : Attribute
    {
        private int[] ints;

        public Attr(int[] ints)
        {
            this.ints = ints;
        }

        public int[] Ints
        {
            get
            {
                return this.ints;
            }
        }
    }


    [Attr(new int[] { 1, 2 })]
    public class Target
    {
    }
}

namespace Test
{
    using Data;

    class Program
    {
        static int Main()
        {
            Class target = (Class) TypeNode.GetTypeNode(typeof(Data.Target));
            Program.Assert(target != null, "The target class was null");

            Program.Assert(target.Attributes[0] != null, "The attribute was null");
            Attr attribute = (Attr) target.Attributes[0].GetRuntimeAttribute();
            Program.Assert(attribute != null, "GetRuntimeAttribute returned null");

            Program.Assert(attribute.Ints != null, "The Ints array was null");
            Program.AssertArray(attribute.Ints, 1, 2);

            return 0;
        }

        static void AssertArray(Array array, params object[] expectedValues)
        {
            Program.Assert(array.GetLength(0) == expectedValues.Length, "The arrays lengths are differents");
            for (int i = 0; i < expectedValues.Length; i++)
            {
                Program.Assert(array.GetValue(i).Equals(expectedValues[i]), "Unexpected values");
            }
        }

        static void Assert(bool condition, string format, params object[] arg)
        {
            if (!condition)
            {
                Console.WriteLine("Test Failure: " + format, arg);
                Environment.Exit(1);
            }
        }
    }
}

