using System;
using System.Text;

class Program {
  static void Main(string[] args) {
      try {
        var t = new Tape();
        t.Run(args);
      } catch (System.Exception e) {
        Console.WriteLine("Error:{0}", e.Message);
        Console.WriteLine("Trace:{0}", e.StackTrace);
      }
  }
}

