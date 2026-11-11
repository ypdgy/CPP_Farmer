// Main must become top-level statements: a fresh save has no 'functions' unlock,
// and a `def`-wrapped Main would make the language unusable at the start.
class Entry {
    static void Main(string[] args) {
        print("start");
        move(North);
    }
}
