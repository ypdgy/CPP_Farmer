// Comprehensive positive case for the C# subset: static fields and methods,
// arrays, foreach, string concatenation, Math, booleans, var inference.
class Basic {
    static int counter = 0;

    static int Add(int a, int b) {
        return a + b;
    }

    static void Main(string[] args) {
        int n = Add(3, 4);
        counter = n;

        int[] xs = new int[5];
        for (int i = 0; i < 5; i++) {
            xs[i] = i * 2;
        }

        var total = 0;
        foreach (var v in xs) {
            total = total + v;
        }

        double root = Math.Sqrt(64);
        bool big = total > 10 && root > 0;
        string msg = "total=" + total;
        Console.WriteLine(msg);
        Console.WriteLine("big=" + big, "root=" + root);

        if (big) {
            harvest();
        } else {
            till();
        }

        while (counter > 0) {
            counter = counter - 1;
        }
    }
}
