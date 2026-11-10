/* cfarmer_api.h - IDE-only declarations for C++ Farmer (C language).
 *
 * This header exists so an editor can complete and describe the game
 * builtins and enum constants while you edit .c files outside the game.
 * The mod's front-end ignores any #include of a *_api.h file, so
 * including it has no effect on the script that actually runs.
 *
 * Generated from the game's builtins.py by scripts/gen_editor_api.py.
 * Do not edit by hand; re-run the script after a game update.
 */
#ifndef CFARMER_API_H
#define CFARMER_API_H

#include <stdbool.h>

/* ---- directions --------------------------------------------------- */
enum { North = 0, East = 1, South = 2, West = 3 };

/* ---- Entities -------------------------------------------------- */
typedef struct {
    int Apple;
    int Bamboo;
    int Bush;
    int Cactus;
    int Carrot;
    int Dead_Pumpkin;
    int Dinosaur;
    int Dynamite;
    int Grass;
    int Hedge;
    int Pumpkin;
    int Rice;
    int Sunflower;
    int Treasure;
    int Tree;
    int Underground_Treasure;
} cf_Entities_t;
static const cf_Entities_t Entities = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

/* ---- Grounds --------------------------------------------------- */
typedef struct {
    int Basalt;
    int Bedrock;
    int Blue_Block;
    int Clay;
    int Coal;
    int Dirt;
    int Dry_Rice_Terrace;
    int Dynamite;
    int Grassland;
    int Green_Block;
    int Iron;
    int Limestone;
    int Loam;
    int Mushroom;
    int Perlite;
    int Petrified_Pumpkin;
    int Quartz;
    int Red_Block;
    int Rice_Terrace;
    int Rock;
    int Sand;
    int Soil;
    int Soot;
    int Treasure_Goal;
    int Treasure_Map;
    int Treasure_Path;
} cf_Grounds_t;
static const cf_Grounds_t Grounds = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25 };

/* ---- Hats ------------------------------------------------------ */
typedef struct {
    int Brown_Hat;
    int Cactus_Hat;
    int Carrot_Hat;
    int Dinosaur_Hat;
    int Dynamite_Hat;
    int Gold_Hat;
    int Gold_Trophy_Hat;
    int Golden_Cactus_Hat;
    int Golden_Carrot_Hat;
    int Golden_Gold_Hat;
    int Golden_Pumpkin_Hat;
    int Golden_Sunflower_Hat;
    int Golden_Tree_Hat;
    int Gray_Hat;
    int Green_Hat;
    int Mining_Hat;
    int Pumpkin_Hat;
    int Purple_Hat;
    int Silver_Trophy_Hat;
    int Straw_Hat;
    int Sunflower_Hat;
    int The_Farmers_Remains;
    int Top_Hat;
    int Traffic_Cone;
    int Traffic_Cone_Stack;
    int Tree_Hat;
    int Wizard_Hat;
    int Wood_Trophy_Hat;
} cf_Hats_t;
static const cf_Hats_t Hats = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27 };

/* ---- Items ----------------------------------------------------- */
typedef struct {
    int Bamboo;
    int Block;
    int Bone;
    int Cactus;
    int Carrot;
    int Coal;
    int Dynamite;
    int Fertilizer;
    int Gold;
    int Hay;
    int Iron;
    int Mushroom;
    int Piggy;
    int Power;
    int Pumpkin;
    int Quartz;
    int Rice;
    int Water;
    int Weird_Substance;
    int Wood;
} cf_Items_t;
static const cf_Items_t Items = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };

/* ---- Leaderboards ---------------------------------------------- */
typedef struct {
    int Cactus;
    int Cactus_Single;
    int Carrots;
    int Carrots_Single;
    int Dinosaur;
    int Fastest_Reset;
    int Hay;
    int Hay_Single;
    int Maze;
    int Maze_Single;
    int Pumpkins;
    int Pumpkins_Single;
    int Sunflowers;
    int Sunflowers_Single;
    int Wood;
    int Wood_Single;
} cf_Leaderboards_t;
static const cf_Leaderboards_t Leaderboards = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

/* ---- Unlocks --------------------------------------------------- */
typedef struct {
    int Auto_Unlock;
    int Bamboo;
    int Cactus;
    int Unlock;
    int Upgrade;
    int Carrots;
    int Coal;
    int Costs;
    int Debug;
    int Debug_2;
    int Debug_Place;
    int Dictionaries;
    int Dinosaurs;
    int Dynamite;
    int Expand;
    int Fertilizer;
    int Functions;
    int Grass;
    int Hats;
    int Import;
    int Iron;
    int Jump;
    int Leaderboard;
    int Lists;
    int Loops;
    int Mazes;
    int Megafarm;
    int Mining;
    int Mushrooms;
    int Ore_Boost;
    int Operators;
    int Petrified_Pumpkins;
    int Plant;
    int Polyculture;
    int Prospecting;
    int Pumpkins;
    int Pyramid;
    int Quartz;
    int Rice;
    int Senses;
    int Simulation;
    int Special_Soils;
    int Speed;
    int Sunflowers;
    int The_Farmers_Remains;
    int Timing;
    int Top_Hat;
    int Treasure_Map;
    int Trees;
    int Underground_Senses;
    int Utilities;
    int Variables;
    int Watering;
} cf_Unlocks_t;
static const cf_Unlocks_t Unlocks = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52 };

/* ---- world & movement --------------------------------------------- */
bool move(int direction);
bool can_move(int direction);
int get_pos_x(void);
int get_pos_y(void);
int get_pos_z(int direction);
int get_world_size(void);
int get_entity_type(int direction);
int get_ground_type(int direction);
bool harvest(void);
bool can_harvest(void);
bool plant(int entity);
void till(void);
bool dig(void);
double get_water(void);
void clear(void);
bool swap(int direction);
bool push(int direction);
bool can_push(int direction);
bool place(int ground);
bool hover(double height);
int get_hover(void);
int measure(int direction);
double get_hardness(int direction);
double get_stability(int direction);
int prospect_iron(void);
double prospect_quartz(void);
bool use_item(int item, int n);
double num_items(int item);
void change_hat(int hat);
int get_companion(void);
double get_time(void);
int get_tick_count(void);
void set_execution_speed(double speed);
void set_world_size(int size);
int get_cost(int thing, int level);
bool unlock(int unlock);
int num_unlocked(int thing);
void jump(int unlock);
double random(void);
int max_drones(void);
int num_drones(void);
int spawn_drone(int task);   /* returns a drone handle */
int wait_for(int drone);
bool has_finished(int drone);
void do_a_flip(void);
void pet_the_piggy(void);
void leaderboard_run(int leaderboard, const char* file_name, double speedup);
double min(double a, double b);
double max(double a, double b);

/* ---- output ------------------------------------------------------- */
/* print and quick_print accept any argument list and return nothing.
 *
 * No prototype can say that portably: `void print();` is "unspecified"
 * in C89 but "no arguments" to C++ and to MSVC's IntelliSense (it shows
 * `void print(void)`), while `const char* first, ...` rejects a call
 * whose first argument is not a string. So C++ gets a variadic
 * template and C gets a variadic macro -- both accept any call without
 * a diagnostic, which is exactly the game's rule. */
#ifdef __cplusplus
template <typename... Args> void print(Args... args);
template <typename... Args> void quick_print(Args... args);
#else
#define print(...) ((void)0)
#define quick_print(...) ((void)0)
#endif

/* ---- C-only ------------------------------------------------------- */
/* printf is not a game builtin: the front-end lowers printf("x=%d", n)
 * into string concatenation at compile time. The prototype matches
 * <stdio.h> so including both headers is harmless. */
int printf(const char* format, ...);

/* get_companion() returns a tuple; store it in an int if you only need
 * the call for its side effect. */

#endif /* CFARMER_API_H */
