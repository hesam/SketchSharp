
namespace ScratchTest {
  public class Program {
    public static void Main(string[] args) {
      OuterCls cls = new OuterCls();
      cls.Foo(42, 3.14159);
    }
  }
  public class OuterCls {
    public void Foo(int x) { }
    public void Foo(int x, int y) { }
  }
}