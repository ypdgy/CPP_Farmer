// Comprehensive positive case for the Java subset: static fields and methods,
// arrays, both loop forms, string concatenation, Math, booleans and branches.
public class Basic {
    static int counter = 0;

    static int add(int a, int b) {
        return a + b;
    }

    public static void main(String[] args) {
        int n = add(3, 4);
        counter = n;

        int[] xs = new int[5];
        for (int i = 0; i < 5; i++) {
            xs[i] = i * 2;
        }

        int total = 0;
        for (int v : xs) {
            total = total + v;
        }

        double root = Math.sqrt(64);
        boolean big = total > 10 && root > 0;
        String msg = "total=" + total;
        System.out.println(msg);
        System.out.println("big=" + big, "root=" + root);

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
