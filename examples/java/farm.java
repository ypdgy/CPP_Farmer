// A Java version of the farm script from main.py.
// Compiled by the C++ Farmer mod into the game's scripting language.
public class Farm {
    static boolean needsWater(double threshold) {
        return get_water() <= threshold;
    }

    static void step() {
        int x = get_pos_x();
        int y = get_pos_y();

        if (y >= 5 && get_ground_type() == Grounds.Grassland) {
            till();
        }

        if (get_entity_type() == None) {
            if (y >= 5) {
                plant(Entities.Tree);
            } else {
                plant(Entities.Sunflower);
            }
        } else if (can_harvest()) {
            harvest();
            plant(Entities.Sunflower);
        }

        double threshold = 0.5;
        if (y >= 5) {
            threshold = 0.75;
        }
        if (needsWater(threshold)) {
            use_item(Items.Water);
        }

        if (x < get_world_size() - 1) {
            move(East);
        } else {
            move(North);
        }
    }

    public static void main(String[] args) {
        while (true) {
            step();
        }
    }
}
