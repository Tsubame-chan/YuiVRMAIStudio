#include "YuiAivisStyleBertRuntime.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <cstdio>
#include <fstream>
#include <iterator>
#include <limits>
#include <map>
#include <memory>
#include <mutex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

#if defined(__APPLE__)
#include <TargetConditionals.h>
#endif

#if __has_include("../../StreamingAssets/YuiLocalAI/Aivis/Runtime/ONNXRuntime/include/onnxruntime_cxx_api.h")
#include "../../StreamingAssets/YuiLocalAI/Aivis/Runtime/ONNXRuntime/include/onnxruntime_cxx_api.h"
#elif __has_include("../../../Data/Raw/YuiLocalAI/Aivis/Runtime/ONNXRuntime/include/onnxruntime_cxx_api.h")
#include "../../../Data/Raw/YuiLocalAI/Aivis/Runtime/ONNXRuntime/include/onnxruntime_cxx_api.h"
#else
#error "onnxruntime_cxx_api.h was not found for Aivis native runtime."
#endif

#if defined(__ANDROID__)
#include "voicevox_core.h"
#define YUI_AIVIS_HAS_VOICEVOX_FRONTEND 1
#elif defined(__APPLE__)
#if TARGET_OS_IPHONE && !TARGET_OS_SIMULATOR
#include "../iOS/Voicevox/voicevox_core.xcframework/ios-arm64/voicevox_core.framework/Headers/voicevox_core.h"
#define YUI_AIVIS_HAS_VOICEVOX_FRONTEND 1
#endif
#endif

namespace yui { namespace aivis {
namespace {

struct PhoneSymbol {
    const char* kana;
    const char* consonant;
    const char* vowel;
};

constexpr float kDefaultStyleWeight = 1.0f;
constexpr float kDefaultSdpRatio = 0.2f;
constexpr float kDefaultNoise = 0.6f;
constexpr float kDefaultNoiseW = 0.8f;
constexpr int64_t kJapaneseLanguageId = 1;
constexpr int64_t kJapaneseToneStart = 6;
constexpr int kBertHiddenSize = 1024;

void LogNativeStep(const char* step) {
    std::fprintf(stderr, "[YuiAivisNative] %s\n", step);
    std::fflush(stderr);
}

std::string Base64Encode(const std::vector<uint8_t>& data);

bool ShouldUseTransientMobileResources() {
#if defined(__APPLE__) && TARGET_OS_IPHONE && !TARGET_OS_SIMULATOR
    return true;
#else
    return false;
#endif
}

constexpr PhoneSymbol kMoraSymbols[] = {
    {"ヴォ", "v", "o"}, {"ヴェ", "v", "e"}, {"ヴィ", "v", "i"}, {"ヴァ", "v", "a"}, {"ヴ", "v", "u"},
    {"リョ", "ry", "o"}, {"リュ", "ry", "u"}, {"リャ", "ry", "a"}, {"リェ", "ry", "e"},
    {"ミョ", "my", "o"}, {"ミュ", "my", "u"}, {"ミャ", "my", "a"}, {"ミェ", "my", "e"},
    {"フュ", "fy", "u"}, {"フォ", "f", "o"}, {"フェ", "f", "e"}, {"フィ", "f", "i"}, {"ファ", "f", "a"},
    {"ピョ", "py", "o"}, {"ピュ", "py", "u"}, {"ピャ", "py", "a"}, {"ピェ", "py", "e"},
    {"ビョ", "by", "o"}, {"ビュ", "by", "u"}, {"ビャ", "by", "a"}, {"ビェ", "by", "e"},
    {"ヒョ", "hy", "o"}, {"ヒュ", "hy", "u"}, {"ヒャ", "hy", "a"}, {"ヒェ", "hy", "e"},
    {"ニョ", "ny", "o"}, {"ニュ", "ny", "u"}, {"ニャ", "ny", "a"}, {"ニェ", "ny", "e"},
    {"ドゥ", "d", "u"}, {"トゥ", "t", "u"}, {"デョ", "dy", "o"}, {"デュ", "dy", "u"}, {"デャ", "dy", "a"}, {"デェ", "dy", "e"},
    {"ディ", "d", "i"}, {"テョ", "ty", "o"}, {"テュ", "ty", "u"}, {"テャ", "ty", "a"}, {"ティ", "t", "i"},
    {"ツォ", "ts", "o"}, {"ツェ", "ts", "e"}, {"ツィ", "ts", "i"}, {"ツァ", "ts", "a"},
    {"チョ", "ch", "o"}, {"チュ", "ch", "u"}, {"チャ", "ch", "a"}, {"チェ", "ch", "e"},
    {"ジョ", "j", "o"}, {"ジュ", "j", "u"}, {"ジャ", "j", "a"}, {"ジェ", "j", "e"},
    {"ショ", "sh", "o"}, {"シュ", "sh", "u"}, {"シャ", "sh", "a"}, {"シェ", "sh", "e"},
    {"グヮ", "gw", "a"}, {"グォ", "gw", "o"}, {"グェ", "gw", "e"}, {"グゥ", "gw", "u"}, {"グィ", "gw", "i"}, {"グァ", "gw", "a"},
    {"クヮ", "kw", "a"}, {"クォ", "kw", "o"}, {"クェ", "kw", "e"}, {"クゥ", "kw", "u"}, {"クィ", "kw", "i"}, {"クァ", "kw", "a"},
    {"ギョ", "gy", "o"}, {"ギュ", "gy", "u"}, {"ギャ", "gy", "a"}, {"ギェ", "gy", "e"},
    {"キョ", "ky", "o"}, {"キュ", "ky", "u"}, {"キャ", "ky", "a"}, {"キェ", "ky", "e"},
    {"ウォ", "w", "o"}, {"ウェ", "w", "e"}, {"ウィ", "w", "i"}, {"イェ", "y", "e"},
    {"ヂョ", "j", "o"}, {"ヂュ", "j", "u"}, {"ヂャ", "j", "a"}, {"ヂェ", "j", "e"},
    {"ン", "", "N"}, {"ワ", "w", "a"}, {"ロ", "r", "o"}, {"レ", "r", "e"}, {"ル", "r", "u"}, {"リ", "r", "i"}, {"ラ", "r", "a"},
    {"ヨ", "y", "o"}, {"ユ", "y", "u"}, {"ヤ", "y", "a"}, {"モ", "m", "o"}, {"メ", "m", "e"}, {"ム", "m", "u"}, {"ミ", "m", "i"}, {"マ", "m", "a"},
    {"ポ", "p", "o"}, {"ボ", "b", "o"}, {"ホ", "h", "o"}, {"ペ", "p", "e"}, {"ベ", "b", "e"}, {"ヘ", "h", "e"}, {"プ", "p", "u"}, {"ブ", "b", "u"}, {"フ", "f", "u"},
    {"ピ", "p", "i"}, {"ビ", "b", "i"}, {"ヒ", "h", "i"}, {"パ", "p", "a"}, {"バ", "b", "a"}, {"ハ", "h", "a"},
    {"ノ", "n", "o"}, {"ネ", "n", "e"}, {"ヌ", "n", "u"}, {"ニ", "n", "i"}, {"ナ", "n", "a"},
    {"ド", "d", "o"}, {"ト", "t", "o"}, {"デ", "d", "e"}, {"テ", "t", "e"}, {"ツ", "ts", "u"}, {"ッ", "", "q"}, {"チ", "ch", "i"},
    {"ダ", "d", "a"}, {"タ", "t", "a"}, {"ゾ", "z", "o"}, {"ソ", "s", "o"}, {"ゼ", "z", "e"}, {"セ", "s", "e"}, {"ズィ", "z", "i"}, {"ズ", "z", "u"}, {"スィ", "s", "i"}, {"ス", "s", "u"},
    {"ジ", "j", "i"}, {"シ", "sh", "i"}, {"ザ", "z", "a"}, {"サ", "s", "a"}, {"ゴ", "g", "o"}, {"コ", "k", "o"}, {"ゲ", "g", "e"}, {"ケ", "k", "e"}, {"グ", "g", "u"}, {"ク", "k", "u"},
    {"ギ", "g", "i"}, {"キ", "k", "i"}, {"ガ", "g", "a"}, {"カ", "k", "a"}, {"オ", "", "o"}, {"エ", "", "e"}, {"ウ", "", "u"}, {"イ", "", "i"}, {"ア", "", "a"},
    {"ヲ", "", "o"}, {"ヱ", "", "e"}, {"ヰ", "", "i"}, {"ヮ", "w", "a"}, {"ョ", "y", "o"}, {"ュ", "y", "u"}, {"ャ", "y", "a"}, {"ヅ", "z", "u"}, {"ヂ", "j", "i"},
    {"ォ", "", "o"}, {"ェ", "", "e"}, {"ゥ", "", "u"}, {"ィ", "", "i"}, {"ァ", "", "a"},
};

std::string EscapeJson(const std::string& value) {
    std::ostringstream out;
    for (unsigned char ch : value) {
        switch (ch) {
            case '\\': out << "\\\\"; break;
            case '"': out << "\\\""; break;
            case '\n': out << "\\n"; break;
            case '\r': out << "\\r"; break;
            case '\t': out << "\\t"; break;
            default:
                if (ch < 0x20) {
                    out << "\\u00";
                    const char* hex = "0123456789abcdef";
                    out << hex[(ch >> 4) & 0x0f] << hex[ch & 0x0f];
                } else {
                    out << static_cast<char>(ch);
                }
                break;
        }
    }
    return out.str();
}

std::string JsonString(const std::string& value) {
    return "\"" + EscapeJson(value) + "\"";
}

std::string ErrorJson(const std::string& code, const std::string& message) {
    return "{\"ok\":false,\"error_code\":" + JsonString(code)
        + ",\"error_message\":" + JsonString(message) + "}";
}

std::string ExtractString(const std::string& json, const std::string& key) {
    const std::string needle = "\"" + key + "\"";
    const size_t keyPos = json.find(needle);
    if (keyPos == std::string::npos) {
        return "";
    }
    const size_t colonPos = json.find(':', keyPos + needle.size());
    if (colonPos == std::string::npos) {
        return "";
    }
    const size_t quoteStart = json.find('"', colonPos + 1);
    if (quoteStart == std::string::npos) {
        return "";
    }
    std::string value;
    bool escaping = false;
    for (size_t i = quoteStart + 1; i < json.size(); ++i) {
        const char ch = json[i];
        if (escaping) {
            value.push_back(ch == 'n' ? '\n' : ch == 'r' ? '\r' : ch == 't' ? '\t' : ch);
            escaping = false;
        } else if (ch == '\\') {
            escaping = true;
        } else if (ch == '"') {
            return value;
        } else {
            value.push_back(ch);
        }
    }
    return "";
}

double ExtractDouble(const std::string& json, const std::string& key, double fallback) {
    const std::string needle = "\"" + key + "\"";
    const size_t keyPos = json.find(needle);
    if (keyPos == std::string::npos) {
        return fallback;
    }
    const size_t colonPos = json.find(':', keyPos + needle.size());
    if (colonPos == std::string::npos) {
        return fallback;
    }
    char* end = nullptr;
    const double value = std::strtod(json.c_str() + colonPos + 1, &end);
    return end == json.c_str() + colonPos + 1 ? fallback : value;
}

int ExtractInt(const std::string& json, const std::string& key, int fallback) {
    return static_cast<int>(std::llround(ExtractDouble(json, key, fallback)));
}

std::vector<uint8_t> ReadBinaryFile(const std::string& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) {
        throw std::runtime_error("Failed to open file: " + path);
    }
    return std::vector<uint8_t>(std::istreambuf_iterator<char>(input), {});
}

std::map<std::string, int64_t> LoadVocab(const std::string& path) {
    std::ifstream input(path);
    if (!input) {
        throw std::runtime_error("Failed to open vocab: " + path);
    }
    std::map<std::string, int64_t> vocab;
    std::string line;
    int64_t index = 0;
    while (std::getline(input, line)) {
        if (!line.empty() && line.back() == '\r') {
            line.pop_back();
        }
        vocab[line] = index++;
    }
    return vocab;
}

std::shared_ptr<const std::map<std::string, int64_t>> CachedVocab(const std::string& path) {
    static std::mutex mutex;
    static std::map<std::string, std::shared_ptr<const std::map<std::string, int64_t>>> cache;
    std::lock_guard<std::mutex> lock(mutex);
    const auto found = cache.find(path);
    if (found != cache.end()) {
        return found->second;
    }
    auto vocab = std::make_shared<const std::map<std::string, int64_t>>(LoadVocab(path));
    cache[path] = vocab;
    return vocab;
}

uint32_t DecodeUtf8At(const std::string& text, size_t& i) {
    const unsigned char c = static_cast<unsigned char>(text[i++]);
    if (c < 0x80) {
        return c;
    }
    if ((c >> 5) == 0x6 && i < text.size()) {
        return ((c & 0x1f) << 6) | (static_cast<unsigned char>(text[i++]) & 0x3f);
    }
    if ((c >> 4) == 0xe && i + 1 < text.size()) {
        uint32_t value = (c & 0x0f) << 12;
        value |= (static_cast<unsigned char>(text[i++]) & 0x3f) << 6;
        value |= static_cast<unsigned char>(text[i++]) & 0x3f;
        return value;
    }
    if ((c >> 3) == 0x1e && i + 2 < text.size()) {
        uint32_t value = (c & 0x07) << 18;
        value |= (static_cast<unsigned char>(text[i++]) & 0x3f) << 12;
        value |= (static_cast<unsigned char>(text[i++]) & 0x3f) << 6;
        value |= static_cast<unsigned char>(text[i++]) & 0x3f;
        return value;
    }
    return 0xfffd;
}

std::string EncodeUtf8(uint32_t cp) {
    std::string out;
    if (cp < 0x80) {
        out.push_back(static_cast<char>(cp));
    } else if (cp < 0x800) {
        out.push_back(static_cast<char>(0xc0 | (cp >> 6)));
        out.push_back(static_cast<char>(0x80 | (cp & 0x3f)));
    } else if (cp < 0x10000) {
        out.push_back(static_cast<char>(0xe0 | (cp >> 12)));
        out.push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3f)));
        out.push_back(static_cast<char>(0x80 | (cp & 0x3f)));
    } else {
        out.push_back(static_cast<char>(0xf0 | (cp >> 18)));
        out.push_back(static_cast<char>(0x80 | ((cp >> 12) & 0x3f)));
        out.push_back(static_cast<char>(0x80 | ((cp >> 6) & 0x3f)));
        out.push_back(static_cast<char>(0x80 | (cp & 0x3f)));
    }
    return out;
}

std::vector<std::string> Utf8Characters(const std::string& text) {
    std::vector<std::string> chars;
    for (size_t i = 0; i < text.size();) {
        const size_t start = i;
        DecodeUtf8At(text, i);
        chars.push_back(text.substr(start, i - start));
    }
    return chars;
}

std::string NormalizeKanaText(const std::string& text) {
    std::string out;
    for (size_t i = 0; i < text.size();) {
        uint32_t cp = DecodeUtf8At(text, i);
        if (cp >= 0x3041 && cp <= 0x3096) {
            cp += 0x60;
        } else if (cp == 0x3001) {
            cp = ',';
        } else if (cp == 0x3002) {
            cp = '.';
        } else if (cp == 0xff01) {
            cp = '!';
        } else if (cp == 0xff1f) {
            cp = '?';
        }
        if (cp == ' ' || cp == '\n' || cp == '\r' || cp == '\t') {
            continue;
        }
        out += EncodeUtf8(cp);
    }
    return out;
}

std::string KatakanaToHiraganaForBert(const std::string& text) {
    std::string out;
    for (size_t i = 0; i < text.size();) {
        uint32_t cp = DecodeUtf8At(text, i);
        if (cp >= 0x30a1 && cp <= 0x30f6) {
            cp -= 0x60;
        }
        out += EncodeUtf8(cp);
    }
    return out;
}

std::string NormalizeBertReadingText(const std::string& text) {
    return KatakanaToHiraganaForBert(NormalizeKanaText(text));
}

int64_t SymbolId(const std::string& symbol) {
    static const std::map<std::string, int64_t> ids = {
        {"_", 0}, {"N", 5}, {"a", 8}, {"a:", 9}, {"b", 19}, {"by", 20}, {"ch", 22}, {"d", 23}, {"dy", 25},
        {"e", 26}, {"e:", 27}, {"f", 34}, {"g", 35}, {"gy", 36}, {"h", 37}, {"hy", 39}, {"i", 40}, {"i:", 42},
        {"j", 55}, {"k", 57}, {"ky", 58}, {"m", 60}, {"my", 61}, {"n", 62}, {"ny", 64}, {"o", 65}, {"o:", 66},
        {"p", 71}, {"py", 72}, {"q", 73}, {"r", 74}, {"ry", 75}, {"s", 76}, {"sh", 77}, {"t", 78}, {"ts", 80},
        {"ty", 81}, {"u", 82}, {"u:", 83}, {"v", 93}, {"w", 97}, {"y", 99}, {"z", 100}, {"zy", 102},
        {"!", 103}, {"?", 104}, {"…", 105}, {",", 106}, {".", 107}, {"'", 108}, {"-", 109}, {"SP", 110}, {"UNK", 111},
    };
    const auto found = ids.find(symbol);
    if (found == ids.end()) {
        return 111;
    }
    return found->second;
}

struct TextFeatures {
    std::string bertText;
    std::vector<int64_t> phones;
    std::vector<int64_t> tones;
    std::vector<int64_t> languages;
    std::vector<int> word2ph;
    std::string lastVowel;
};

std::vector<std::string> UnsupportedPhoneFallback(const std::string& consonant);
void AppendPhoneSymbol(TextFeatures& features, const std::string& symbol, int tone, int& phoneCount);
void AppendMoraPhones(TextFeatures& features, const PhoneSymbol& matched, int tone, int& phoneCount);

bool StartsWithAt(const std::string& value, size_t offset, const char* needle) {
    const size_t len = std::strlen(needle);
    return offset + len <= value.size() && value.compare(offset, len, needle) == 0;
}

TextFeatures BuildTextFeatures(const std::string& text) {
    const std::string normalized = NormalizeKanaText(text);
    TextFeatures features;
    features.bertText = NormalizeBertReadingText(text);
    features.phones.push_back(SymbolId("_"));
    features.tones.push_back(kJapaneseToneStart);
    features.languages.push_back(kJapaneseLanguageId);
    features.word2ph.push_back(1);

    std::vector<int> phoneCountsByChar(Utf8Characters(normalized).size(), 0);
    size_t charIndex = 0;
    for (size_t offset = 0; offset < normalized.size();) {
        const PhoneSymbol* matched = nullptr;
        size_t matchedLen = 0;
        for (const auto& symbol : kMoraSymbols) {
            if (StartsWithAt(normalized, offset, symbol.kana)) {
                matched = &symbol;
                matchedLen = std::strlen(symbol.kana);
                break;
            }
        }

        if (matched != nullptr) {
            int phoneCount = 0;
            AppendMoraPhones(features, *matched, 0, phoneCount);
            if (charIndex < phoneCountsByChar.size()) {
                phoneCountsByChar[charIndex] += phoneCount;
            }
            const auto chars = Utf8Characters(normalized.substr(offset, matchedLen));
            charIndex += std::max<size_t>(1, chars.size());
            offset += matchedLen;
            continue;
        }

        size_t next = offset;
        const uint32_t cp = DecodeUtf8At(normalized, next);
        std::string punctuation;
        if (cp == ',' || cp == '.') {
            punctuation = std::string(1, static_cast<char>(cp));
        } else if (cp == '!' || cp == '?' || cp == '-' || cp == '\'') {
            punctuation = std::string(1, static_cast<char>(cp));
        } else if (cp == 0x2026) {
            punctuation = "…";
        }
        if (!punctuation.empty()) {
            features.phones.push_back(SymbolId(punctuation));
            features.tones.push_back(kJapaneseToneStart);
            features.languages.push_back(kJapaneseLanguageId);
            if (charIndex < phoneCountsByChar.size()) {
                phoneCountsByChar[charIndex] += 1;
            }
        }
        ++charIndex;
        offset = next;
    }

    features.phones.push_back(SymbolId("_"));
    features.tones.push_back(kJapaneseToneStart);
    features.languages.push_back(kJapaneseLanguageId);

    for (int count : phoneCountsByChar) {
        features.word2ph.push_back(count);
    }
    features.word2ph.push_back(1);
    if (features.phones.size() < 3) {
        throw std::runtime_error("Aivis text frontend could not produce phones. Use kana/reading text for the current native runtime.");
    }
    return features;
}

struct MoraItem {
    std::string text;
    std::string consonant;
    std::string vowel;
};

std::string ExtractJsonStringField(const std::string& json, size_t objectStart, size_t objectEnd, const std::string& key) {
    const std::string needle = "\"" + key + "\"";
    const size_t keyPos = json.find(needle, objectStart);
    if (keyPos == std::string::npos || keyPos > objectEnd) {
        return "";
    }
    const size_t colonPos = json.find(':', keyPos + needle.size());
    if (colonPos == std::string::npos || colonPos > objectEnd) {
        return "";
    }
    const size_t quoteStart = json.find('"', colonPos + 1);
    if (quoteStart == std::string::npos || quoteStart > objectEnd) {
        return "";
    }
    std::string value;
    bool escaping = false;
    for (size_t i = quoteStart + 1; i <= objectEnd && i < json.size(); ++i) {
        const char ch = json[i];
        if (escaping) {
            value.push_back(ch == 'n' ? '\n' : ch == 'r' ? '\r' : ch == 't' ? '\t' : ch);
            escaping = false;
        } else if (ch == '\\') {
            escaping = true;
        } else if (ch == '"') {
            return value;
        } else {
            value.push_back(ch);
        }
    }
    return "";
}

int ExtractJsonIntField(const std::string& json, size_t objectStart, size_t objectEnd, const std::string& key, int fallback) {
    const std::string needle = "\"" + key + "\"";
    const size_t keyPos = json.find(needle, objectStart);
    if (keyPos == std::string::npos || keyPos > objectEnd) {
        return fallback;
    }
    const size_t colonPos = json.find(':', keyPos + needle.size());
    if (colonPos == std::string::npos || colonPos > objectEnd) {
        return fallback;
    }
    char* end = nullptr;
    const long value = std::strtol(json.c_str() + colonPos + 1, &end, 10);
    if (end == json.c_str() + colonPos + 1 || static_cast<size_t>(end - json.c_str()) > objectEnd + 1) {
        return fallback;
    }
    return static_cast<int>(value);
}

size_t FindMatchingBracket(const std::string& json, size_t openPos, char openChar, char closeChar) {
    int depth = 0;
    bool inString = false;
    bool escaping = false;
    for (size_t i = openPos; i < json.size(); ++i) {
        const char ch = json[i];
        if (inString) {
            if (escaping) {
                escaping = false;
            } else if (ch == '\\') {
                escaping = true;
            } else if (ch == '"') {
                inString = false;
            }
            continue;
        }
        if (ch == '"') {
            inString = true;
        } else if (ch == openChar) {
            ++depth;
        } else if (ch == closeChar) {
            --depth;
            if (depth == 0) {
                return i;
            }
        }
    }
    return std::string::npos;
}

void AddPhoneFromMora(TextFeatures& features, const MoraItem& mora, int tone, int& phoneCount) {
    const std::string normalized = NormalizeKanaText(mora.text);
    if (normalized.empty()) {
        return;
    }

    bool onlyLongVowels = true;
    for (const auto& ch : Utf8Characters(normalized)) {
        if (ch != "ー") {
            onlyLongVowels = false;
            break;
        }
    }
    if (onlyLongVowels) {
        const std::string repeatPhone = features.lastVowel.empty() ? "-" : features.lastVowel;
        for (size_t i = 0; i < Utf8Characters(normalized).size(); ++i) {
            AppendPhoneSymbol(features, repeatPhone, tone, phoneCount);
        }
        return;
    }

    std::string punctuation;
    if (normalized == "、" || normalized == "，") {
        punctuation = ",";
    } else if (normalized == "。" || normalized == "．") {
        punctuation = ".";
    } else if (normalized == "！") {
        punctuation = "!";
    } else if (normalized == "？") {
        punctuation = "?";
    } else if (normalized == "…" || normalized == "," || normalized == "." || normalized == "!" || normalized == "?") {
        punctuation = normalized;
    }
    if (!punctuation.empty()) {
        AppendPhoneSymbol(features, punctuation, tone, phoneCount);
        return;
    }

    const PhoneSymbol* matched = nullptr;
    for (const auto& symbol : kMoraSymbols) {
        if (normalized == symbol.kana) {
            matched = &symbol;
            break;
        }
    }
    if (matched == nullptr) {
        AppendPhoneSymbol(features, "UNK", tone, phoneCount);
        return;
    }

    AppendMoraPhones(features, *matched, tone, phoneCount);
}

std::vector<std::string> UnsupportedPhoneFallback(const std::string& consonant) {
    // Match Style-Bert-VITS2's convert_unsupported_phones_for_current_model().
    if (consonant == "kw") {
        return {"k", "u", "w"};
    }
    if (consonant == "gw") {
        return {"g", "u", "w"};
    }
    if (consonant == "fy") {
        return {"hy"};
    }
    return {consonant};
}

void AppendPhoneSymbol(TextFeatures& features, const std::string& symbol, int tone, int& phoneCount) {
    features.phones.push_back(SymbolId(symbol));
    features.tones.push_back(kJapaneseToneStart + tone);
    features.languages.push_back(kJapaneseLanguageId);
    ++phoneCount;
}

void AppendMoraPhones(TextFeatures& features, const PhoneSymbol& matched, int tone, int& phoneCount) {
    if (matched.consonant[0] != '\0') {
        for (const auto& symbol : UnsupportedPhoneFallback(matched.consonant)) {
            AppendPhoneSymbol(features, symbol, tone, phoneCount);
        }
    }
    AppendPhoneSymbol(features, matched.vowel, tone, phoneCount);
    features.lastVowel = matched.vowel;
}

std::vector<int> DistributePhoneCount(int phoneCount, size_t tokenCount) {
    if (tokenCount == 0) {
        return {};
    }
    std::vector<int> counts(tokenCount, 0);
    for (int i = 0; i < phoneCount; ++i) {
        counts[static_cast<size_t>(i) % tokenCount] += 1;
    }
    return counts;
}

void AppendMoraFeature(TextFeatures& features, const MoraItem& mora, int tone) {
    int phoneCount = 0;
    AddPhoneFromMora(features, mora, tone, phoneCount);
    if (phoneCount <= 0) {
        return;
    }
    const std::string bertText = NormalizeBertReadingText(mora.text);
    features.bertText += bertText;
    const auto tokenCounts = DistributePhoneCount(phoneCount, Utf8Characters(bertText).size());
    features.word2ph.insert(features.word2ph.end(), tokenCounts.begin(), tokenCounts.end());
}

TextFeatures BuildTextFeaturesFromAudioQuery(const std::string& audioQueryJson) {
    TextFeatures features;
    features.phones.push_back(SymbolId("_"));
    features.tones.push_back(kJapaneseToneStart);
    features.languages.push_back(kJapaneseLanguageId);
    features.word2ph.push_back(1);

    size_t phraseSearch = 0;
    while (true) {
        const size_t morasKey = audioQueryJson.find("\"moras\"", phraseSearch);
        if (morasKey == std::string::npos) {
            break;
        }
        const size_t arrayOpen = audioQueryJson.find('[', morasKey);
        if (arrayOpen == std::string::npos) {
            break;
        }
        const size_t arrayClose = FindMatchingBracket(audioQueryJson, arrayOpen, '[', ']');
        if (arrayClose == std::string::npos) {
            break;
        }
        const size_t phraseOpen = audioQueryJson.rfind('{', morasKey);
        if (phraseOpen == std::string::npos) {
            break;
        }
        const size_t phraseClose = FindMatchingBracket(audioQueryJson, phraseOpen, '{', '}');
        if (phraseClose == std::string::npos) {
            break;
        }
        const int accent = ExtractJsonIntField(audioQueryJson, phraseOpen, phraseClose, "accent", 1);
        const int accentIndex = std::max(0, accent - 1);

        int moraIndex = 0;
        for (size_t objectOpen = audioQueryJson.find('{', arrayOpen);
             objectOpen != std::string::npos && objectOpen < arrayClose;) {
            const size_t objectClose = FindMatchingBracket(audioQueryJson, objectOpen, '{', '}');
            if (objectClose == std::string::npos || objectClose > arrayClose) {
                break;
            }
            MoraItem mora;
            mora.text = ExtractJsonStringField(audioQueryJson, objectOpen, objectClose, "text");
            mora.consonant = ExtractJsonStringField(audioQueryJson, objectOpen, objectClose, "consonant");
            mora.vowel = ExtractJsonStringField(audioQueryJson, objectOpen, objectClose, "vowel");
            if (mora.text.empty() && mora.vowel.empty()) {
                objectOpen = audioQueryJson.find('{', objectClose + 1);
                continue;
            }
            int tone = 0;
            if (moraIndex == 0 && accentIndex != 0) {
                tone = 0;
            } else if (moraIndex <= accentIndex) {
                tone = 1;
            }
            AppendMoraFeature(features, mora, tone);
            ++moraIndex;
            objectOpen = audioQueryJson.find('{', objectClose + 1);
        }

        const size_t pauseKey = audioQueryJson.find("\"pause_mora\"", arrayClose);
        if (pauseKey != std::string::npos && pauseKey < phraseClose) {
            const size_t pauseOpen = audioQueryJson.find('{', pauseKey);
            if (pauseOpen != std::string::npos && pauseOpen < phraseClose) {
                const size_t pauseClose = FindMatchingBracket(audioQueryJson, pauseOpen, '{', '}');
                if (pauseClose != std::string::npos && pauseClose <= phraseClose) {
                    MoraItem pauseMora;
                    // AivisSpeech's Style-Bert-VITS2 frontend appends a fixed comma
                    // for pauses. VOICEVOX pause mora fields are runtime-specific and
                    // should not be treated as ordinary kana.
                    pauseMora.text = ",";
                    AppendMoraFeature(features, pauseMora, 0);
                }
            }
        }
        phraseSearch = phraseClose + 1;
    }

    features.phones.push_back(SymbolId("_"));
    features.tones.push_back(kJapaneseToneStart);
    features.languages.push_back(kJapaneseLanguageId);
    features.word2ph.push_back(1);

    if (features.phones.size() < 3 || features.bertText.empty()) {
        throw std::runtime_error("VOICEVOX/OpenJTalk frontend did not return usable accent phrases.");
    }
    if (features.word2ph.size() != Utf8Characters(features.bertText).size() + 2) {
        throw std::runtime_error("Aivis BERT text/word2ph length mismatch.");
    }
    int word2phSum = 0;
    for (int count : features.word2ph) {
        word2phSum += count;
    }
    if (word2phSum != static_cast<int>(features.phones.size())) {
        throw std::runtime_error("Aivis word2ph/phone length mismatch.");
    }
    return features;
}

#if defined(YUI_AIVIS_HAS_VOICEVOX_FRONTEND)
const VoicevoxOnnxruntime* gVoicevoxOrt = nullptr;
OpenJtalkRc* gOpenJtalk = nullptr;
VoicevoxSynthesizer* gVoicevoxSynthesizer = nullptr;
std::string gLoadedVoicevoxModelPath;
std::mutex gVoicevoxMutex;

void EnsureVoicevoxFrontend(const std::string& dictPath, const std::string& voicevoxModelPath) {
    if (gVoicevoxSynthesizer == nullptr) {
        LogNativeStep("voicevox_onnxruntime_init_once: begin");
#if defined(VOICEVOX_LOAD_ONNXRUNTIME)
        VoicevoxLoadOnnxruntimeOptions loadOptions = voicevox_make_default_load_onnxruntime_options();
        loadOptions.filename = "libonnxruntime.so";
        VoicevoxResultCode result = voicevox_onnxruntime_load_once(loadOptions, &gVoicevoxOrt);
#else
        VoicevoxResultCode result = voicevox_onnxruntime_init_once(&gVoicevoxOrt);
#endif
        LogNativeStep("voicevox_onnxruntime_init_once: end");
        if (result != VOICEVOX_RESULT_OK || gVoicevoxOrt == nullptr) {
            throw std::runtime_error("Failed to initialize VOICEVOX ONNX Runtime for Aivis frontend.");
        }
        LogNativeStep("voicevox_open_jtalk_rc_new: begin");
        result = voicevox_open_jtalk_rc_new(dictPath.c_str(), &gOpenJtalk);
        LogNativeStep("voicevox_open_jtalk_rc_new: end");
        if (result != VOICEVOX_RESULT_OK || gOpenJtalk == nullptr) {
            throw std::runtime_error("Failed to initialize OpenJTalk for Aivis frontend.");
        }
        VoicevoxInitializeOptions options = voicevox_make_default_initialize_options();
        options.acceleration_mode = VOICEVOX_ACCELERATION_MODE_CPU;
        options.cpu_num_threads = 0;
        LogNativeStep("voicevox_synthesizer_new: begin");
        result = voicevox_synthesizer_new(gVoicevoxOrt, gOpenJtalk, options, &gVoicevoxSynthesizer);
        LogNativeStep("voicevox_synthesizer_new: end");
        if (result != VOICEVOX_RESULT_OK || gVoicevoxSynthesizer == nullptr) {
            throw std::runtime_error("Failed to create VOICEVOX synthesizer for Aivis frontend.");
        }
    }

    if (gLoadedVoicevoxModelPath != voicevoxModelPath) {
        VoicevoxVoiceModelFile* model = nullptr;
        LogNativeStep("voicevox_voice_model_file_open: begin");
        VoicevoxResultCode result = voicevox_voice_model_file_open(voicevoxModelPath.c_str(), &model);
        LogNativeStep("voicevox_voice_model_file_open: end");
        if (result != VOICEVOX_RESULT_OK || model == nullptr) {
            throw std::runtime_error("Failed to open VOICEVOX helper model for Aivis frontend.");
        }
        LogNativeStep("voicevox_synthesizer_load_voice_model: begin");
        result = voicevox_synthesizer_load_voice_model(gVoicevoxSynthesizer, model);
        LogNativeStep("voicevox_synthesizer_load_voice_model: end");
        voicevox_voice_model_file_delete(model);
        if (result != VOICEVOX_RESULT_OK && result != VOICEVOX_RESULT_MODEL_ALREADY_LOADED_ERROR) {
            throw std::runtime_error("Failed to load VOICEVOX helper model for Aivis frontend.");
        }
        gLoadedVoicevoxModelPath = voicevoxModelPath;
    }
}

TextFeatures BuildTextFeaturesWithVoicevox(
    const std::string& text,
    const std::string& dictPath,
    const std::string& voicevoxModelPath,
    int voicevoxSpeakerId) {
    std::lock_guard<std::mutex> lock(gVoicevoxMutex);
    EnsureVoicevoxFrontend(dictPath, voicevoxModelPath);
    char* audioQueryJson = nullptr;
    LogNativeStep("voicevox_synthesizer_create_audio_query: begin");
    VoicevoxResultCode result = voicevox_synthesizer_create_audio_query(
        gVoicevoxSynthesizer,
        text.c_str(),
        voicevoxSpeakerId,
        &audioQueryJson);
    LogNativeStep("voicevox_synthesizer_create_audio_query: end");
    if (result != VOICEVOX_RESULT_OK || audioQueryJson == nullptr) {
        if (audioQueryJson != nullptr) {
            voicevox_json_free(audioQueryJson);
        }
        throw std::runtime_error("VOICEVOX/OpenJTalk audio query failed for Aivis frontend.");
    }
    LogNativeStep("voicevox_audio_query_copy: begin");
    std::string audioQuery(audioQueryJson);
    LogNativeStep("voicevox_audio_query_copy: end");
    LogNativeStep("voicevox_json_free: begin");
    voicevox_json_free(audioQueryJson);
    LogNativeStep("voicevox_json_free: end");
    LogNativeStep("BuildTextFeaturesFromAudioQuery: begin");
    auto features = BuildTextFeaturesFromAudioQuery(audioQuery);
    LogNativeStep("BuildTextFeaturesFromAudioQuery: end");
    return features;
}

void ReleaseVoicevoxFrontend() {
    std::lock_guard<std::mutex> lock(gVoicevoxMutex);
    if (gVoicevoxSynthesizer != nullptr) {
        voicevox_synthesizer_delete(gVoicevoxSynthesizer);
        gVoicevoxSynthesizer = nullptr;
    }
    if (gOpenJtalk != nullptr) {
        voicevox_open_jtalk_rc_delete(gOpenJtalk);
        gOpenJtalk = nullptr;
    }
    gLoadedVoicevoxModelPath.clear();
}

#endif

void AddBlank(TextFeatures& features) {
    auto intersperseInt64 = [](const std::vector<int64_t>& input) {
        std::vector<int64_t> output(input.size() * 2 + 1, 0);
        for (size_t i = 0; i < input.size(); ++i) {
            output[i * 2 + 1] = input[i];
        }
        return output;
    };
    features.phones = intersperseInt64(features.phones);
    features.tones = intersperseInt64(features.tones);
    features.languages = intersperseInt64(features.languages);
    for (int& count : features.word2ph) {
        count *= 2;
    }
    if (!features.word2ph.empty()) {
        features.word2ph[0] += 1;
    }
}

std::vector<int64_t> Tokenize(const std::string& text, const std::map<std::string, int64_t>& vocab) {
    std::vector<int64_t> ids;
    auto tokenId = [&vocab](const std::string& token, int64_t fallback) {
        const auto found = vocab.find(token);
        return found == vocab.end() ? fallback : found->second;
    };
    ids.push_back(tokenId("[CLS]", 1));
    for (const auto& ch : Utf8Characters(text)) {
        ids.push_back(tokenId(ch, 3));
    }
    ids.push_back(tokenId("[SEP]", 2));
    return ids;
}

struct NpyArray {
    std::vector<int64_t> shape;
    std::vector<float> values;
};

NpyArray LoadNpyFloat32(const std::string& path) {
    const auto bytes = ReadBinaryFile(path);
    if (bytes.size() < 16 || bytes[0] != 0x93 || std::memcmp(bytes.data() + 1, "NUMPY", 5) != 0) {
        throw std::runtime_error("Invalid NPY file: " + path);
    }
    const uint8_t major = bytes[6];
    size_t headerLenOffset = 8;
    size_t headerLen = 0;
    if (major == 1) {
        headerLen = bytes[8] | (bytes[9] << 8);
        headerLenOffset = 10;
    } else if (major == 2 || major == 3) {
        headerLen = bytes[8] | (bytes[9] << 8) | (bytes[10] << 16) | (bytes[11] << 24);
        headerLenOffset = 12;
    } else {
        throw std::runtime_error("Unsupported NPY version.");
    }
    if (headerLenOffset + headerLen > bytes.size()) {
        throw std::runtime_error("Invalid NPY header length.");
    }
    const std::string header(reinterpret_cast<const char*>(bytes.data() + headerLenOffset), headerLen);
    if (header.find("'descr': '<f4'") == std::string::npos && header.find("\"descr\": \"<f4\"") == std::string::npos) {
        throw std::runtime_error("Only little-endian float32 NPY files are supported.");
    }
    const size_t open = header.find('(');
    const size_t close = header.find(')', open);
    if (open == std::string::npos || close == std::string::npos) {
        throw std::runtime_error("NPY shape was not found.");
    }
    std::vector<int64_t> shape;
    std::stringstream shapeStream(header.substr(open + 1, close - open - 1));
    std::string item;
    while (std::getline(shapeStream, item, ',')) {
        if (item.find_first_not_of(" \t") == std::string::npos) {
            continue;
        }
        shape.push_back(std::stoll(item));
    }
    size_t count = 1;
    for (int64_t dim : shape) {
        count *= static_cast<size_t>(dim);
    }
    const size_t dataOffset = headerLenOffset + headerLen;
    if (dataOffset + count * sizeof(float) > bytes.size()) {
        throw std::runtime_error("NPY data is truncated.");
    }
    NpyArray array;
    array.shape = std::move(shape);
    array.values.resize(count);
    std::memcpy(array.values.data(), bytes.data() + dataOffset, count * sizeof(float));
    return array;
}

std::shared_ptr<const NpyArray> CachedNpyFloat32(const std::string& path) {
    static std::mutex mutex;
    static std::map<std::string, std::shared_ptr<const NpyArray>> cache;
    std::lock_guard<std::mutex> lock(mutex);
    const auto found = cache.find(path);
    if (found != cache.end()) {
        return found->second;
    }
    auto array = std::make_shared<const NpyArray>(LoadNpyFloat32(path));
    cache[path] = array;
    return array;
}

float Clamp(float value, float low, float high) {
    return std::max(low, std::min(high, value));
}

float StyleWeightFromIntonation(float intonationScale) {
    if (intonationScale >= 0.0f && intonationScale <= 1.0f) {
        return intonationScale * kDefaultStyleWeight;
    }
    if (intonationScale > 1.0f && intonationScale <= 2.0f) {
        return kDefaultStyleWeight + (intonationScale - 1.0f) * (10.0f - kDefaultStyleWeight);
    }
    return kDefaultStyleWeight;
}

std::vector<float> NormalizeAivisOutput(std::vector<float> audio) {
    float maxAbs = 0.0f;
    for (float sample : audio) {
        maxAbs = std::max(maxAbs, std::abs(sample));
    }
    if (maxAbs > 2.0f) {
        for (float& sample : audio) {
            sample /= 32768.0f;
        }
    }
    return audio;
}

std::vector<float> TrimSilence(const std::vector<float>& audio, float threshold = 0.0004f) {
    size_t start = 0;
    while (start < audio.size() && std::abs(audio[start]) <= threshold) {
        ++start;
    }
    if (start >= audio.size()) {
        return {};
    }

    size_t end = audio.size();
    while (end > start && std::abs(audio[end - 1]) <= threshold) {
        --end;
    }
    return std::vector<float>(audio.begin() + static_cast<std::ptrdiff_t>(start), audio.begin() + static_cast<std::ptrdiff_t>(end));
}

std::vector<float> AddSilence(
    const std::vector<float>& audio,
    int sampleRate,
    float speedScale,
    float prePhonemeLength,
    float postPhonemeLength) {
    const float safeSpeed = std::max(0.1f, speedScale);
    const size_t preSamples = static_cast<size_t>(std::max(0.0f, prePhonemeLength) * sampleRate / safeSpeed);
    const size_t postSamples = static_cast<size_t>(std::max(0.0f, postPhonemeLength) * sampleRate / safeSpeed);
    std::vector<float> result;
    result.reserve(preSamples + audio.size() + postSamples);
    result.insert(result.end(), preSamples, 0.0f);
    result.insert(result.end(), audio.begin(), audio.end());
    result.insert(result.end(), postSamples, 0.0f);
    return result;
}

std::vector<float> LoadStyleVector(const std::string& path, int styleId, float weight) {
    const auto npy = CachedNpyFloat32(path);
    if (npy->shape.size() != 2 || npy->shape[1] != 256) {
        throw std::runtime_error("Aivis style vector shape must be [num_styles, 256].");
    }
    const int64_t rows = npy->shape[0];
    if (styleId < 0 || styleId >= rows) {
        styleId = 0;
    }
    std::vector<float> style(256);
    const float* mean = npy->values.data();
    const float* selected = npy->values.data() + static_cast<size_t>(styleId) * 256;
    for (size_t i = 0; i < 256; ++i) {
        style[i] = mean[i] + (selected[i] - mean[i]) * weight;
    }
    return style;
}

std::string Base64Encode(const std::vector<uint8_t>& data) {
    static constexpr char table[] = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    std::string out;
    out.reserve(((data.size() + 2) / 3) * 4);
    for (size_t i = 0; i < data.size(); i += 3) {
        const uint32_t a = data[i];
        const uint32_t b = i + 1 < data.size() ? data[i + 1] : 0;
        const uint32_t c = i + 2 < data.size() ? data[i + 2] : 0;
        const uint32_t triple = (a << 16) | (b << 8) | c;
        out.push_back(table[(triple >> 18) & 0x3f]);
        out.push_back(table[(triple >> 12) & 0x3f]);
        out.push_back(i + 1 < data.size() ? table[(triple >> 6) & 0x3f] : '=');
        out.push_back(i + 2 < data.size() ? table[triple & 0x3f] : '=');
    }
    return out;
}

void AppendLE16(std::vector<uint8_t>& out, int16_t value) {
    out.push_back(static_cast<uint8_t>(value & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 8) & 0xff));
}

void AppendLE32(std::vector<uint8_t>& out, uint32_t value) {
    out.push_back(static_cast<uint8_t>(value & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 8) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 16) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 24) & 0xff));
}

std::vector<uint8_t> MakeWav(const float* samples, size_t count, int sampleRate, float volume) {
    std::vector<uint8_t> wav;
    const uint32_t dataBytes = static_cast<uint32_t>(count * sizeof(int16_t));
    wav.insert(wav.end(), {'R', 'I', 'F', 'F'});
    AppendLE32(wav, 36 + dataBytes);
    wav.insert(wav.end(), {'W', 'A', 'V', 'E', 'f', 'm', 't', ' '});
    AppendLE32(wav, 16);
    AppendLE16(wav, 1);
    AppendLE16(wav, 1);
    AppendLE32(wav, static_cast<uint32_t>(sampleRate));
    AppendLE32(wav, static_cast<uint32_t>(sampleRate * sizeof(int16_t)));
    AppendLE16(wav, sizeof(int16_t));
    AppendLE16(wav, 16);
    wav.insert(wav.end(), {'d', 'a', 't', 'a'});
    AppendLE32(wav, dataBytes);
    for (size_t i = 0; i < count; ++i) {
        const float scaled = Clamp(samples[i] * volume, -1.0f, 1.0f);
        AppendLE16(wav, static_cast<int16_t>(std::lrint(scaled * 32767.0f)));
    }
    return wav;
}

Ort::Env& OrtEnv() {
    static Ort::Env env(ORT_LOGGING_LEVEL_WARNING, "YuiAivis");
    return env;
}

Ort::SessionOptions MakeSessionOptions() {
    Ort::SessionOptions options;
    options.SetGraphOptimizationLevel(GraphOptimizationLevel::ORT_DISABLE_ALL);
    options.SetIntraOpNumThreads(1);
    options.SetInterOpNumThreads(1);
    if (ShouldUseTransientMobileResources()) {
        options.DisableCpuMemArena();
        options.DisableMemPattern();
    }
    return options;
}

struct CachedOrtSession {
    explicit CachedOrtSession(const std::string& path)
        : session(OrtEnv(), path.c_str(), MakeSessionOptions()) {}

    Ort::Session session;
    std::mutex runMutex;
};

std::shared_ptr<CachedOrtSession> CachedSession(const std::string& path) {
    if (ShouldUseTransientMobileResources()) {
        return std::make_shared<CachedOrtSession>(path);
    }

    static std::mutex mutex;
    static std::map<std::string, std::shared_ptr<CachedOrtSession>> cache;
    std::lock_guard<std::mutex> lock(mutex);
    const auto found = cache.find(path);
    if (found != cache.end()) {
        return found->second;
    }
    auto session = std::make_shared<CachedOrtSession>(path);
    cache[path] = session;
    return session;
}

std::vector<float> RunBert(
    const std::string& modelPath,
    const std::string& vocabPath,
    const TextFeatures& features) {
    const auto vocab = CachedVocab(vocabPath);
    const auto inputIds = Tokenize(features.bertText, *vocab);
    if (features.word2ph.size() != inputIds.size()) {
        throw std::runtime_error("BERT word2ph/token length mismatch.");
    }
    std::vector<int64_t> attention(inputIds.size(), 1);
    std::vector<int64_t> inputShape = {1, static_cast<int64_t>(inputIds.size())};

    const auto session = CachedSession(modelPath);
    Ort::MemoryInfo memory = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);
    std::vector<Ort::Value> inputs;
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, const_cast<int64_t*>(inputIds.data()), inputIds.size(), inputShape.data(), inputShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, attention.data(), attention.size(), inputShape.data(), inputShape.size()));

    const char* inputNames[] = {"input_ids", "attention_mask"};
    const char* outputNames[] = {"output"};
    std::vector<Ort::Value> outputs;
    {
        std::lock_guard<std::mutex> lock(session->runMutex);
        outputs = session->session.Run(Ort::RunOptions{nullptr}, inputNames, inputs.data(), inputs.size(), outputNames, 1);
    }
    auto info = outputs[0].GetTensorTypeAndShapeInfo();
    const auto shape = info.GetShape();
    if (shape.size() != 2 || shape[1] != kBertHiddenSize) {
        throw std::runtime_error("Unexpected Japanese BERT output shape.");
    }
    const float* raw = outputs[0].GetTensorData<float>();
    const int64_t tokenCount = shape[0];
    if (tokenCount != static_cast<int64_t>(features.word2ph.size())) {
        throw std::runtime_error("Japanese BERT output/token length mismatch.");
    }

    const int64_t phoneCount = static_cast<int64_t>(features.phones.size());
    std::vector<float> phoneBert(static_cast<size_t>(kBertHiddenSize * phoneCount), 0.0f);
    int64_t phoneIndex = 0;
    for (size_t token = 0; token < features.word2ph.size(); ++token) {
        const int repeat = features.word2ph[token];
        for (int r = 0; r < repeat; ++r) {
            if (phoneIndex >= phoneCount) {
                break;
            }
            for (int dim = 0; dim < kBertHiddenSize; ++dim) {
                phoneBert[static_cast<size_t>(dim * phoneCount + phoneIndex)] = raw[token * kBertHiddenSize + dim];
            }
            ++phoneIndex;
        }
    }
    if (phoneIndex != phoneCount) {
        throw std::runtime_error("BERT phone-level feature length mismatch.");
    }
    return phoneBert;
}

std::vector<float> RunAivis(
    const std::string& modelPath,
    const TextFeatures& features,
    const std::vector<float>& bert,
    const std::vector<float>& styleVector,
    int speakerId,
    float lengthScale,
    float sdpRatio,
    float noiseScale,
    float noiseScaleW) {
    const auto session = CachedSession(modelPath);
    Ort::MemoryInfo memory = Ort::MemoryInfo::CreateCpu(OrtArenaAllocator, OrtMemTypeDefault);

    std::vector<int64_t> seqShape = {1, static_cast<int64_t>(features.phones.size())};
    std::vector<int64_t> lengthShape = {1};
    std::vector<int64_t> bertShape = {1, kBertHiddenSize, static_cast<int64_t>(features.phones.size())};
    std::vector<int64_t> styleShape = {1, 256};
    int64_t xLength = static_cast<int64_t>(features.phones.size());
    int64_t sid = speakerId;

    std::vector<Ort::Value> inputs;
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, const_cast<int64_t*>(features.phones.data()), features.phones.size(), seqShape.data(), seqShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, &xLength, 1, lengthShape.data(), lengthShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, &sid, 1, lengthShape.data(), lengthShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, const_cast<int64_t*>(features.tones.data()), features.tones.size(), seqShape.data(), seqShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<int64_t>(memory, const_cast<int64_t*>(features.languages.data()), features.languages.size(), seqShape.data(), seqShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<float>(memory, const_cast<float*>(bert.data()), bert.size(), bertShape.data(), bertShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<float>(memory, const_cast<float*>(styleVector.data()), styleVector.size(), styleShape.data(), styleShape.size()));
    inputs.push_back(Ort::Value::CreateTensor<float>(memory, &lengthScale, 1, nullptr, 0));
    inputs.push_back(Ort::Value::CreateTensor<float>(memory, &sdpRatio, 1, nullptr, 0));
    inputs.push_back(Ort::Value::CreateTensor<float>(memory, &noiseScale, 1, nullptr, 0));
    inputs.push_back(Ort::Value::CreateTensor<float>(memory, &noiseScaleW, 1, nullptr, 0));

    const char* inputNames[] = {
        "x_tst", "x_tst_lengths", "sid", "tones", "language", "bert",
        "style_vec", "length_scale", "sdp_ratio", "noise_scale", "noise_scale_w",
    };
    const char* outputNames[] = {"output"};
    std::vector<Ort::Value> outputs;
    {
        std::lock_guard<std::mutex> lock(session->runMutex);
        outputs = session->session.Run(Ort::RunOptions{nullptr}, inputNames, inputs.data(), inputs.size(), outputNames, 1);
    }
    auto info = outputs[0].GetTensorTypeAndShapeInfo();
    const auto shape = info.GetShape();
    size_t sampleCount = 1;
    for (int64_t dim : shape) {
        if (dim > 0) {
            sampleCount *= static_cast<size_t>(dim);
        }
    }
    const float* output = outputs[0].GetTensorData<float>();
    return std::vector<float>(output, output + sampleCount);
}

}  // namespace

bool StyleBertRuntimeHasJapaneseTextFrontend() {
#if defined(YUI_AIVIS_HAS_VOICEVOX_FRONTEND)
    return true;
#else
    return false;
#endif
}

std::string SynthesizeStyleBertJson(const std::string& requestJson) {
    try {
        const std::string text = ExtractString(requestJson, "text");
        const std::string modelPath = ExtractString(requestJson, "model_path");
        const std::string styleVectorsPath = ExtractString(requestJson, "style_vectors_path");
        const std::string bertModelPath = ExtractString(requestJson, "bert_model_path");
        const std::string bertVocabPath = ExtractString(requestJson, "bert_vocab_path");
        const std::string openJtalkDictPath = ExtractString(requestJson, "open_jtalk_dict_path");
        const std::string voicevoxModelPath = ExtractString(requestJson, "voicevox_model_path");
        const int voicevoxSpeakerId = ExtractInt(requestJson, "voicevox_speaker_id", 14);
        const int speakerId = ExtractInt(requestJson, "speaker_id", 0);
        const int styleId = ExtractInt(requestJson, "style_id", 0);
        const int sampleRate = ExtractInt(requestJson, "sampling_rate", 44100);
        const float speedScale = Clamp(static_cast<float>(ExtractDouble(requestJson, "speed_scale", 1.0)), 0.1f, 2.0f);
        const float volumeScale = Clamp(static_cast<float>(ExtractDouble(requestJson, "volume_scale", 1.0)), 0.0f, 2.0f);
        const float intonationScale = Clamp(static_cast<float>(ExtractDouble(requestJson, "intonation_scale", 1.0)), 0.0f, 2.0f);
        const float prePhonemeLength = Clamp(static_cast<float>(ExtractDouble(requestJson, "pre_phoneme_length", 0.1)), 0.0f, 1.5f);
        const float postPhonemeLength = Clamp(static_cast<float>(ExtractDouble(requestJson, "post_phoneme_length", 0.1)), 0.0f, 1.5f);
        const float lengthScale = 1.0f / std::max(0.1f, speedScale);
        const float styleWeight = StyleWeightFromIntonation(intonationScale);

#if defined(YUI_AIVIS_HAS_VOICEVOX_FRONTEND)
        LogNativeStep("BuildTextFeaturesWithVoicevox: begin");
        auto features = BuildTextFeaturesWithVoicevox(text, openJtalkDictPath, voicevoxModelPath, voicevoxSpeakerId);
        LogNativeStep("BuildTextFeaturesWithVoicevox: end");
        if (ShouldUseTransientMobileResources()) {
            LogNativeStep("ReleaseVoicevoxFrontend: begin");
            ReleaseVoicevoxFrontend();
            LogNativeStep("ReleaseVoicevoxFrontend: end");
        }
#else
        auto features = BuildTextFeatures(text);
#endif
        LogNativeStep("AddBlank: begin");
        AddBlank(features);
        LogNativeStep("AddBlank: end");
        LogNativeStep("RunBert: begin");
        const auto bert = RunBert(bertModelPath, bertVocabPath, features);
        LogNativeStep("RunBert: end");
        LogNativeStep("LoadStyleVector: begin");
        const auto styleVector = LoadStyleVector(styleVectorsPath, styleId, styleWeight);
        LogNativeStep("LoadStyleVector: end");
        LogNativeStep("RunAivis: begin");
        auto audio = RunAivis(
            modelPath,
            features,
            bert,
            styleVector,
            speakerId,
            lengthScale,
            kDefaultSdpRatio,
            kDefaultNoise,
            kDefaultNoiseW);
        LogNativeStep("RunAivis: end");
        audio = AddSilence(
            TrimSilence(NormalizeAivisOutput(std::move(audio))),
            sampleRate,
            speedScale,
            prePhonemeLength,
            postPhonemeLength);
        LogNativeStep("MakeWav: begin");
        const auto wav = MakeWav(audio.data(), audio.size(), sampleRate, volumeScale);
        LogNativeStep("MakeWav: end");
        const int durationMs = sampleRate > 0
            ? static_cast<int>((audio.size() * 1000) / static_cast<size_t>(sampleRate))
            : 0;

        std::ostringstream json;
        json << "{\"ok\":true"
             << ",\"audio_base64\":" << JsonString(Base64Encode(wav))
             << ",\"sample_rate\":" << sampleRate
             << ",\"duration_ms\":" << durationMs
             << "}";
        return json.str();
    } catch (const Ort::Exception& ex) {
        return ErrorJson("onnxruntime_error", ex.what());
    } catch (const std::exception& ex) {
        return ErrorJson("style_bert_runtime_error", ex.what());
    }
}

} }  // namespace yui::aivis
