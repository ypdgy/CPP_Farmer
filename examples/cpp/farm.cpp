// A C++ version of the farm script from main.py.
// Compiled by cppfarmer into the game's scripting language.
#include <iostream>
#include <vector>

struct Cell {
    int x;
    int y;
};

enum Zone {
    ZONE_EDGE,
    ZONE_TREE,
    ZONE_PUMPKIN,
    ZONE_PLAIN
};

// Decide which crops a column should grow.
Zone zoneOf(int x) {
    if (x == 0 || x == 2) return ZONE_EDGE;
    if (x == 1 || x == 3 || x == 6) return ZONE_TREE;
    if (x == 4 || x == 5) return ZONE_PUMPKIN;
    return ZONE_PLAIN;
}

bool needsWater(double threshold) {
    return get_water() <= threshold;
}

void plantFor(Zone z) {
    if (z == ZONE_EDGE) {
        if (get_pos_y() >= 5) {
            plant(Entities.Tree);
        } else {
            plant(Entities.Sunflower);
        }
        return;
    }
    if (z == ZONE_TREE) {
        if (get_ground_type() == Grounds.Grassland) {
            plant(Entities.Tree);
        } else {
            plant(Entities.Carrot);
        }
        return;
    }
    if (z == ZONE_PUMPKIN) {
        plant(Entities.Pumpkin);
        return;
    }
}

void tillIfNeeded(Zone z) {
    if (z == ZONE_EDGE && get_pos_y() <= 4 && get_ground_type() == Grounds.Grassland) {
        till();
    }
    if (z == ZONE_TREE && get_pos_y() >= 3 && get_ground_type() == Grounds.Grassland) {
        till();
    }
    if (z == ZONE_PUMPKIN && get_ground_type() == Grounds.Grassland) {
        till();
    }
}

void step() {
    int x = get_pos_x();
    Zone z = zoneOf(x);

    tillIfNeeded(z);

    if (get_entity_type() == None) {
        plantFor(z);
    } else if (can_harvest()) {
        harvest();
        plantFor(z);
    }

    double threshold = z == ZONE_PUMPKIN ? 0.75 : 0.5;
    if (needsWater(threshold)) {
        use_item(Items.Water);
    }

    // Patrol pattern: east until the edge, then shift one row north.
    if (x < get_world_size() - 1) {
        move(East);
    } else {
        move(North);
    }
}

int main() {
    for (int i = 0; i < 1; i++) {
        while (true) {
            step();
        }
    }
}
