#pragma once

#include <string>

namespace yui { namespace aivis {

std::string GetStatusJson(const std::string& requestJson, bool nativeRuntimeLinked);
std::string SynthesizeJson(const std::string& requestJson);

} }  // namespace yui::aivis
