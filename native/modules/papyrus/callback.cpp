#include <RE/B/BSScript_IStackCallbackFunctor.h>

namespace RE::BSScript
{

// This CommonLib revision declares the empty interface destructor without a
// definition. Supply it here so owned callbacks can be destroyed normally,
// without modifying the external submodule or calling an engine destructor.
IStackCallbackFunctor::~IStackCallbackFunctor() = default;

} // namespace RE::BSScript
