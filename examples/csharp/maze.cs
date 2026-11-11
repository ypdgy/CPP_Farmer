// C# 版 maze：移动到 (0,0)，施肥，然后深度优先搜索找宝藏。
// 原 Python 版拆成两个文件（maze.py 用 `import dfs` 调 dfs.dfs()）；C# 不支持
// 跨文件导入，所以把 dfs 内联成一个 static 方法。
class Maze {
    // 方向表，代替 Python 的 {1:North, 2:East, 3:West, 4:South}
    static int[] dirs = {North, East, West, South};

    // 移动历史栈，代替 Python 的 [0]
    static int[] had_moved = new int[1];

    static int Dfs() {
        if (get_entity_type() == Entities.Treasure) {
            harvest();
            return 1;
        }
        for (int i = 1; i <= 4; i++) {
            int d = dirs[i - 1];
            if (can_move(d) && (5 - i != had_moved[had_moved.Length - 1])) {
                move(d);
                append(had_moved, i);
                if (Dfs() == 1) {
                    return 1;
                }
            }
        }
        move(dirs[4 - had_moved[had_moved.Length - 1]]);
        pop(had_moved);
        return 0;
    }

    static void Main(string[] args) {
        int x = get_pos_x();
        while (x != 0) {
            move(West);
            x -= 1;
        }
        int y = get_pos_y();
        while (y != 0) {
            move(South);
            y -= 1;
        }
        int size = get_world_size();
        double substance = size * Math.Pow(2, num_unlocked(Unlocks.Mazes) - 1);
        int a = 0;
        while (a < 1) {
            harvest();
            plant(Entities.Bush);
            a += 1;
            use_item(Items.Weird_Substance, substance);
            Dfs();
        }
    }
}
