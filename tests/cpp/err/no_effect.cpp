// Must fail: a bare name as a statement does nothing. The game only warns about
// its own builtins ("did you mean print()?"), so printf -- a name only this mod
// knows -- used to compile into a silent no-op.
int main() { printf; return 0; }
