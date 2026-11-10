// Feature smoke test for the front-end. Not meant to be run in-game.
#include <iostream>
#include <vector>

struct Point {
    int x;
    int y;
};

enum Mode { MODE_IDLE, MODE_RUN };

int counter = 0;
std::vector<int> trace;

bool isEdge(int v, int limit) {
    return v == 0 || v == limit - 1;
}

int clampi(int v, int lo, int hi) {
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}

void collect(int n) {
    int i = 0;
    while (i < n) {
        i++;
        if (i % 2 == 0) continue;
        append(trace, i);
        if (i > 8) break;
    }
    for (int k = n; k > 0; k--) {
        counter += k;
    }
}

void report(Point p, Mode m) {
    std::cout << "point " << p.x << "," << p.y << " mode " << m << "\n";
    bool inside = p.x >= 0 && p.y >= 0;
    int pick = inside ? 1 : 0;
    std::cout << "pick=" << pick << " half=" << p.x / 2 << " real=" << p.x / 2.0 << "\n";
}

int main() {
    Point origin = {0, 0};
    origin.x = 3;
    int grid[9];
    grid[4] = clampi(origin.x, 1, 2);

    collect(10);
    report(origin, MODE_RUN);

    for (int v : trace) {
        counter += v;
    }
    std::cout << "counter=" << counter << " size=" << len(trace) << "\n";
}
