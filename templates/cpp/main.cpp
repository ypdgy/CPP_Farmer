// Starter program. Edit this file, then run scripts\watch_to_save.bat
// and press F5 in-game (with "file watcher" enabled in the options).
#include <iostream>

bool tileReady() {
    return get_entity_type() != None && can_harvest();
}

int main() {
    int n = get_world_size();

    for (int row = 0; row < n; row++) {
        for (int col = 0; col < n; col++) {
            if (get_ground_type() == Grounds.Grassland) {
                till();
            }
            if (tileReady()) {
                harvest();
            }
            if (get_entity_type() == None) {
                plant(Entities.Bush);
            }
            if (col < n - 1) {
                move(East);
            }
        }
        if (row < n - 1) {
            move(North);
        }
    }
    std::cout << "pass done\n";
}
