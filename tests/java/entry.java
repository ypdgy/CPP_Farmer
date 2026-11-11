// main must become top-level statements: a fresh save has no 'functions' unlock,
// and a `def`-wrapped main would make the language unusable at the start.
public class Entry {
    public static void main(String[] args) {
        print("start");
        move(North);
    }
}
