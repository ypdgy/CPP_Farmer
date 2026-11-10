#include "cf_util.h"

#include <cstdlib>
#include <cstring>

char* cf_dup(const std::string& s) {
    char* p = static_cast<char*>(std::malloc(s.size() + 1));
    if (!p) return nullptr;
    std::memcpy(p, s.c_str(), s.size() + 1);
    return p;
}

std::vector<std::string> cf_split_csv(const char* csv) {
    std::vector<std::string> out;
    if (!csv) return out;
    std::string cur;
    for (const char* p = csv; *p; ++p) {
        char c = *p;
        if (c == ',' || c == ';' || c == ' ' || c == '\t') {
            if (!cur.empty()) {
                out.push_back(cur);
                cur.clear();
            }
            continue;
        }
        cur.push_back(c);
    }
    if (!cur.empty()) out.push_back(cur);
    return out;
}
