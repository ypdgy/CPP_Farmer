// Java subset front-end.
//
// Transpiles a small, deliberately closed Java subset into the game's own
// scripting language. The lexer and the AST are shared with the C++ front-end
// (Java's syntax is a C-family subset, so the tokens and node shapes are the
// same); only the parser and the code generator are Java-specific.
#ifndef CF_FRONTEND_JAVA_H
#define CF_FRONTEND_JAVA_H

#include "cf_api.h"

// Descriptor registered in native/src/registry.cpp.
const CfLanguage& cf_java_language();

#endif  // CF_FRONTEND_JAVA_H
