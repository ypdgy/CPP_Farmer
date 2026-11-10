// Structs, arrays, enums and loops, all in C syntax.
#include <stdio.h>
#include <stdbool.h>

typedef struct {
    int x;
    int y;
} Point;

enum Color { Red, Green, Blue };

struct Cell {
    int value;
};

int add(int a, int b) {
    return a + b;
}

int main(void) {
    Point p;
    p.x = 3;
    p.y = 4;

    struct Cell c;
    c.value = 7;

    int grid[4];
    for (int i = 0; i < 4; i++) {
        grid[i] = i * i;
    }

    enum Color favorite = Green;
    int shade = Blue;

    printf("p=(%d,%d) cell=%d\n", p.x, p.y, c.value);
    printf("grid3=%d sum=%d\n", grid[3], add(p.x, p.y));
    printf("favorite=%d shade=%d\n", favorite, shade);

    int total = 0;
    int i = 0;
    while (i < 4) {
        total += grid[i];
        i++;
    }
    printf("total=%d\n", total);
    return 0;
}
