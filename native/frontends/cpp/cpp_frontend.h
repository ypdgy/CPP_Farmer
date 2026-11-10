// C++ subset front-end.
//
// Transpiles a small, deliberately closed C++ subset into the game's own
// scripting language. Lexer/parser/codegen live next to this file and know
// nothing about the plugin.
#ifndef CF_FRONTEND_CPP_H
#define CF_FRONTEND_CPP_H

#include "cf_api.h"

// Descriptor registered in native/src/registry.cpp.
const CfLanguage& cf_cpp_language();

#endif  // CF_FRONTEND_CPP_H
