#pragma once

#include <string>

namespace yui { namespace aivis {

std::string SynthesizeStyleBertJson(const std::string& requestJson);
bool StyleBertRuntimeHasJapaneseTextFrontend();

} }  // namespace yui::aivis
