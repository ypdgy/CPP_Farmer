// Small helpers shared by the registry and the front-ends.
#ifndef CF_UTIL_H
#define CF_UTIL_H

#include <string>
#include <vector>

// Copies a string into memory owned by the caller (released with cf_free).
// Returns null only when allocation fails.
char* cf_dup(const std::string& s);

// Splits "a,b;c d" into {"a","b","c","d"}, dropping empty fields.
std::vector<std::string> cf_split_csv(const char* csv);

#endif  // CF_UTIL_H
