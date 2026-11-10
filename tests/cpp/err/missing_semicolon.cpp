// Must fail: the ';' after a using-directive is mandatory. Before the fix the
// parser ran to EOF here, silently dropping every statement below it, so the
// script did nothing and reported no error.
using namespace std

int main() { return 0; }
