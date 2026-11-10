// printf: every supported conversion is expanded at compile time.
#include <stdio.h>

int main(void) {
    int n = 42;
    double pi = 3.14159;
    printf("int=%d\n", n);
    printf("float=%f\n", pi);
    printf("string=%s\n", "ok");
    printf("char=%c\n", 'A');
    printf("percent=100%%\n");
    printf("multi=%d %d %d\n", 1, 2, 3);
    printf("literal only\n");
    return 0;
}
