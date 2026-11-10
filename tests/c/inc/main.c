// Multi-file: the included .c becomes a module import, not a text merge.
#include <stdio.h>
#include "helper.c"

int main(void) {
    printf("helper=%d\n", helper_add(2, 3));
    return 0;
}
