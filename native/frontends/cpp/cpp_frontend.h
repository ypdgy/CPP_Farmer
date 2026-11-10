// C++ subset front-end.
//
// Transpiles a small, deliberately closed C++ subset into the game's own
// scripting language. Lexer/parser/codegen live next to this file and know
// nothing about the plugin.
#ifndef CF_FRONTEND_CPP_H
#define CF_FRONTEND_CPP_H

#include "cf_api.h"

// Descriptors registered in native/src/registry.cpp. Both share the parser and
// the code generator; only the dialect flag differs.
const CfLanguage& cf_cpp_language();
const CfLanguage& cf_c_language();

#endif  // CF_FRONTEND_CPP_H
