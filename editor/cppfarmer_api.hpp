// cppfarmer_api.hpp - IDE-only declarations for C++ Farmer.
//
// This header exists so VS Code / clangd can complete and describe the
// game's builtin functions and enum constants while you edit .cpp files
// outside the game. The mod's front-end ignores `#include <...>` entirely,
// so including it has no effect on the script that actually runs.
//
// Generated from the game's builtins.py by scripts/gen_editor_api.py.
// Do not edit by hand; re-run the script after a game update.
#pragma once

#include <string>
#include <vector>
#include <map>
#include <iostream>

// ---- directions ----------------------------------------------------
const int North = 0;
const int East = 1;
const int South = 2;
const int West = 3;

// ---- Entities --------------------------------------------------
struct Entities_t {
    static const int Apple = 0;
    static const int Bamboo = 1;
    static const int Bush = 2;
    static const int Cactus = 3;
    static const int Carrot = 4;
    static const int Dead_Pumpkin = 5;
    static const int Dinosaur = 6;
    static const int Dynamite = 7;
    static const int Grass = 8;
    static const int Hedge = 9;
    static const int Pumpkin = 10;
    static const int Rice = 11;
    static const int Sunflower = 12;
    static const int Treasure = 13;
    static const int Tree = 14;
    static const int Underground_Treasure = 15;
};
inline const Entities_t Entities{};

// ---- Grounds ---------------------------------------------------
struct Grounds_t {
    static const int Basalt = 0;
    static const int Bedrock = 1;
    static const int Blue_Block = 2;
    static const int Clay = 3;
    static const int Coal = 4;
    static const int Dirt = 5;
    static const int Dry_Rice_Terrace = 6;
    static const int Dynamite = 7;
    static const int Grassland = 8;
    static const int Green_Block = 9;
    static const int Iron = 10;
    static const int Limestone = 11;
    static const int Loam = 12;
    static const int Mushroom = 13;
    static const int Perlite = 14;
    static const int Petrified_Pumpkin = 15;
    static const int Quartz = 16;
    static const int Red_Block = 17;
    static const int Rice_Terrace = 18;
    static const int Rock = 19;
    static const int Sand = 20;
    static const int Soil = 21;
    static const int Soot = 22;
    static const int Treasure_Goal = 23;
    static const int Treasure_Map = 24;
    static const int Treasure_Path = 25;
};
inline const Grounds_t Grounds{};

// ---- Hats ------------------------------------------------------
struct Hats_t {
    static const int Brown_Hat = 0;
    static const int Cactus_Hat = 1;
    static const int Carrot_Hat = 2;
    static const int Dinosaur_Hat = 3;
    static const int Dynamite_Hat = 4;
    static const int Gold_Hat = 5;
    static const int Gold_Trophy_Hat = 6;
    static const int Golden_Cactus_Hat = 7;
    static const int Golden_Carrot_Hat = 8;
    static const int Golden_Gold_Hat = 9;
    static const int Golden_Pumpkin_Hat = 10;
    static const int Golden_Sunflower_Hat = 11;
    static const int Golden_Tree_Hat = 12;
    static const int Gray_Hat = 13;
    static const int Green_Hat = 14;
    static const int Mining_Hat = 15;
    static const int Pumpkin_Hat = 16;
    static const int Purple_Hat = 17;
    static const int Silver_Trophy_Hat = 18;
    static const int Straw_Hat = 19;
    static const int Sunflower_Hat = 20;
    static const int The_Farmers_Remains = 21;
    static const int Top_Hat = 22;
    static const int Traffic_Cone = 23;
    static const int Traffic_Cone_Stack = 24;
    static const int Tree_Hat = 25;
    static const int Wizard_Hat = 26;
    static const int Wood_Trophy_Hat = 27;
};
inline const Hats_t Hats{};

// ---- Items -----------------------------------------------------
struct Items_t {
    static const int Bamboo = 0;
    static const int Block = 1;
    static const int Bone = 2;
    static const int Cactus = 3;
    static const int Carrot = 4;
    static const int Coal = 5;
    static const int Dynamite = 6;
    static const int Fertilizer = 7;
    static const int Gold = 8;
    static const int Hay = 9;
    static const int Iron = 10;
    static const int Mushroom = 11;
    static const int Piggy = 12;
    static const int Power = 13;
    static const int Pumpkin = 14;
    static const int Quartz = 15;
    static const int Rice = 16;
    static const int Water = 17;
    static const int Weird_Substance = 18;
    static const int Wood = 19;
};
inline const Items_t Items{};

// ---- Leaderboards ----------------------------------------------
struct Leaderboards_t {
    static const int Cactus = 0;
    static const int Cactus_Single = 1;
    static const int Carrots = 2;
    static const int Carrots_Single = 3;
    static const int Dinosaur = 4;
    static const int Fastest_Reset = 5;
    static const int Hay = 6;
    static const int Hay_Single = 7;
    static const int Maze = 8;
    static const int Maze_Single = 9;
    static const int Pumpkins = 10;
    static const int Pumpkins_Single = 11;
    static const int Sunflowers = 12;
    static const int Sunflowers_Single = 13;
    static const int Wood = 14;
    static const int Wood_Single = 15;
};
inline const Leaderboards_t Leaderboards{};

// ---- Unlocks ---------------------------------------------------
struct Unlocks_t {
    static const int Auto_Unlock = 0;
    static const int Bamboo = 1;
    static const int Cactus = 2;
    static const int Unlock = 3;
    static const int Upgrade = 4;
    static const int Carrots = 5;
    static const int Coal = 6;
    static const int Costs = 7;
    static const int Debug = 8;
    static const int Debug_2 = 9;
    static const int Debug_Place = 10;
    static const int Dictionaries = 11;
    static const int Dinosaurs = 12;
    static const int Dynamite = 13;
    static const int Expand = 14;
    static const int Fertilizer = 15;
    static const int Functions = 16;
    static const int Grass = 17;
    static const int Hats = 18;
    static const int Import = 19;
    static const int Iron = 20;
    static const int Jump = 21;
    static const int Leaderboard = 22;
    static const int Lists = 23;
    static const int Loops = 24;
    static const int Mazes = 25;
    static const int Megafarm = 26;
    static const int Mining = 27;
    static const int Mushrooms = 28;
    static const int Ore_Boost = 29;
    static const int Operators = 30;
    static const int Petrified_Pumpkins = 31;
    static const int Plant = 32;
    static const int Polyculture = 33;
    static const int Prospecting = 34;
    static const int Pumpkins = 35;
    static const int Pyramid = 36;
    static const int Quartz = 37;
    static const int Rice = 38;
    static const int Senses = 39;
    static const int Simulation = 40;
    static const int Special_Soils = 41;
    static const int Speed = 42;
    static const int Sunflowers = 43;
    static const int The_Farmers_Remains = 44;
    static const int Timing = 45;
    static const int Top_Hat = 46;
    static const int Treasure_Map = 47;
    static const int Trees = 48;
    static const int Underground_Senses = 49;
    static const int Utilities = 50;
    static const int Variables = 51;
    static const int Watering = 52;
};
inline const Unlocks_t Unlocks{};

// ---- world & movement ----------------------------------------------
bool move(int direction);
bool can_move(int direction);
int get_pos_x();
int get_pos_y();
int get_pos_z(int direction = 0);
int get_world_size();
int get_entity_type(int direction = 0);
int get_ground_type(int direction = 0);
bool harvest();
bool can_harvest();
bool plant(int entity);
void till();
bool dig();
double get_water();
void clear();
bool swap(int direction);
bool push(int direction);
bool can_push(int direction);
bool place(int ground);
bool hover(double height);
int get_hover();
int measure(int direction = 0);
double get_hardness(int direction = 0);
double get_stability(int direction = 0);
int prospect_iron();
double prospect_quartz();
bool use_item(int item, int n = 1);
double num_items(int item);
void change_hat(int hat);
int get_companion();
double get_time();
int get_tick_count();
void set_execution_speed(double speed);
void set_world_size(int size);
int get_cost(int thing, int level = 0);
bool unlock(int unlock);
int num_unlocked(int thing);
void jump(int unlock);
double random();
int max_drones();
int num_drones();
int spawn_drone(int task);   // returns a drone handle
int wait_for(int drone);
bool has_finished(int drone);
void do_a_flip();
void pet_the_piggy();
void leaderboard_run(int leaderboard, std::string file_name, double speedup);
template <typename... Args> void print(Args... args);
template <typename... Args> void quick_print(Args... args);
double abs(double x);
double min(double a, double b);
double max(double a, double b);

// Game-specific notes for this front-end:
// - `get_companion()` returns (entity, (x, y)); tuple types are not
//   supported here, so the stub returns int. Ignore the value or store
//   it in an int if you only need the call for its side effect.
// - `measure()` / `get_cost()` return containers; see docs/LANGUAGE.md
//   for what the transpiler can actually do with them.
