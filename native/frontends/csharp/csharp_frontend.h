// C# subset front-end.
//
// Transpiles a small, deliberately closed C# subset into the game's own scripting
// language. Like Java, it reuses the C++ front-end's lexer and AST but has its own
// parser and code generator (C# differs from Java in its type names, entry point,
// Console output and Math spelling).
#ifndef CF_FRONTEND_CSHARP_H
#define CF_FRONTEND_CSHARP_H

#include "cf_api.h"

// Descriptor registered in native/src/registry.cpp.
const CfLanguage& cf_csharp_language();

#endif  // CF_FRONTEND_CSHARP_H
