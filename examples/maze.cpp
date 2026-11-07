// Maze solver: the C++ counterpart of dfs.py + maze.py.
#include <iostream>
#include <vector>

std::vector<int> path;          // global: the moves taken so far

// Directions indexed 1..4 so that i and (5 - i) are opposites.
Direction dirOf(int i) {
    if (i == 1) return North;
    if (i == 2) return East;
    if (i == 3) return West;
    return South;
}

bool dfs() {
    if (get_entity_type() == Entities.Treasure) {
        harvest();
        return true;
    }

    for (int i = 1; i <= 4; i++) {
        int back = 5 - i;
        if (can_move(dirOf(i)) && back != path[len(path) - 1]) {
            move(dirOf(i));
            append(path, i);
            if (dfs()) {
                return true;
            }
        }
    }
    move(dirOf(5 - path[len(path) - 1]));
    pop(path);
    return false;
}

void gotoOrigin() {
    while (get_pos_x() != 0) {
        move(West);
    }
    while (get_pos_y() != 0) {
        move(South);
    }
}

int main() {
    gotoOrigin();

    int size = get_world_size();
    int substance = size * pow(2, num_unlocked(Unlocks.Mazes) - 1);

    append(path, 0);
    for (int a = 0; a < 1000; a++) {
        harvest();
        plant(Entities.Bush);
        use_item(Items.Weird_Substance, substance);
        dfs();
    }
}
