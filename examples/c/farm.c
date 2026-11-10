// 9x9 harvest sweep, written as plain C.
// Rename a code window to something.c and press F5.
#include <stdio.h>
#include <stdbool.h>

typedef struct { int x; int y; } Pos;

bool tile_ready(void) {
    return get_entity_type() != None && can_harvest();
}

int main(void) {
    int n = get_world_size();
    int harvested = 0;
    int planted = 0;

    for (int row = 0; row < n; row++) {
        for (int col = 0; col < n; col++) {
            if (tile_ready()) {
                harvest();
                harvested++;
            }
            if (get_entity_type() == None) {
                plant(Entities.Bush);
                planted++;
            }
            if (col < n - 1) {
                move(East);
            }
        }
        if (row < n - 1) {
            move(North);
        }
    }

    printf("harvested=%d planted=%d\n", harvested, planted);
    return 0;
}
