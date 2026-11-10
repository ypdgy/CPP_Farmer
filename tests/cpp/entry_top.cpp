// main must become top-level statements: a fresh save has no 'functions' unlock,
// and a `def main()` would make the whole language unusable at the start.
void main() {
    print("start");
    move(North);
}
