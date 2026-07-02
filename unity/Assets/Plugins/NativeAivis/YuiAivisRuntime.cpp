#include "YuiAivisRuntime.h"
#include "YuiAivisStyleBertRuntime.h"

#include <sys/stat.h>

#include <algorithm>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>

namespace yui { namespace aivis {
namespace {

bool FileExists(const std::string& path) {
    if (path.empty()) {
        return false;
    }

    struct stat info;
    return stat(path.c_str(), &info) == 0 && (info.st_mode & S_IFREG) != 0;
}

bool DirectoryExists(const std::string& path) {
    if (path.empty()) {
        return false;
    }

    struct stat info;
    return stat(path.c_str(), &info) == 0 && (info.st_mode & S_IFDIR) != 0;
}

std::string JoinPath(const std::string& root, const std::string& relative) {
    if (root.empty()) {
        return relative;
    }
    if (root.back() == '/') {
        return root + relative;
    }
    return root + "/" + relative;
}

std::string ExtractManifestString(const std::string& json, const std::string& key) {
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
            value.push_back(ch);
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

bool ManifestArrayContainsString(const std::string& json, const std::string& key, const std::string& value) {
    const std::string needle = "\"" + key + "\"";
    const size_t keyPos = json.find(needle);
    if (keyPos == std::string::npos) {
        return false;
    }
    const size_t arrayOpen = json.find('[', keyPos + needle.size());
    if (arrayOpen == std::string::npos) {
        return false;
    }
    const size_t arrayClose = json.find(']', arrayOpen + 1);
    if (arrayClose == std::string::npos) {
        return false;
    }

    size_t cursor = arrayOpen + 1;
    while (cursor < arrayClose) {
        const size_t quoteStart = json.find('"', cursor);
        if (quoteStart == std::string::npos || quoteStart >= arrayClose) {
            return false;
        }
        std::string item;
        bool escaping = false;
        size_t i = quoteStart + 1;
        for (; i < arrayClose; ++i) {
            const char ch = json[i];
            if (escaping) {
                item.push_back(ch);
                escaping = false;
            } else if (ch == '\\') {
                escaping = true;
            } else if (ch == '"') {
                break;
            } else {
                item.push_back(ch);
            }
        }
        if (item == value) {
            return true;
        }
        cursor = i + 1;
    }
    return false;
}

bool ReadyManifestExists(const std::string& path, const std::string& platform) {
    if (!FileExists(path)) {
        return false;
    }

    std::ifstream input(path);
    std::ostringstream buffer;
    buffer << input.rdbuf();
    const std::string manifest = buffer.str();
    if (ExtractManifestString(manifest, "status") == "ready") {
        return true;
    }
    return !platform.empty() && ManifestArrayContainsString(manifest, "ready_platforms", platform);
}

std::string EscapeJson(const std::string& value) {
    std::ostringstream output;
    for (char ch : value) {
        switch (ch) {
            case '\\':
                output << "\\\\";
                break;
            case '"':
                output << "\\\"";
                break;
            case '\b':
                output << "\\b";
                break;
            case '\f':
                output << "\\f";
                break;
            case '\n':
                output << "\\n";
                break;
            case '\r':
                output << "\\r";
                break;
            case '\t':
                output << "\\t";
                break;
            default:
                if (static_cast<unsigned char>(ch) < 0x20) {
                    output << "\\u00";
                    const char* hex = "0123456789abcdef";
                    output << hex[(ch >> 4) & 0x0f] << hex[ch & 0x0f];
                } else {
                    output << ch;
                }
                break;
        }
    }
    return output.str();
}

std::string JsonString(const std::string& value) {
    return "\"" + EscapeJson(value) + "\"";
}

std::string JsonArray(const std::vector<std::string>& values) {
    std::ostringstream output;
    output << "[";
    for (size_t i = 0; i < values.size(); ++i) {
        if (i > 0) {
            output << ",";
        }
        output << JsonString(values[i]);
    }
    output << "]";
    return output.str();
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
            switch (ch) {
                case '"':
                case '\\':
                case '/':
                    value.push_back(ch);
                    break;
                case 'n':
                    value.push_back('\n');
                    break;
                case 'r':
                    value.push_back('\r');
                    break;
                case 't':
                    value.push_back('\t');
                    break;
                default:
                    value.push_back(ch);
                    break;
            }
            escaping = false;
            continue;
        }

        if (ch == '\\') {
            escaping = true;
            continue;
        }
        if (ch == '"') {
            return value;
        }
        value.push_back(ch);
    }

    return "";
}

void AddUnique(std::vector<std::string>& values, const std::string& value) {
    if (value.empty()) {
        return;
    }
    if (std::find(values.begin(), values.end(), value) == values.end()) {
        values.push_back(value);
    }
}

std::string ErrorJson(
    const std::string& code,
    const std::string& message,
    const std::vector<std::string>& missing = {}) {
    std::ostringstream output;
    output
        << "{\"ok\":false"
        << ",\"error_code\":" << JsonString(code)
        << ",\"error_message\":" << JsonString(message)
        << ",\"missing_components\":" << JsonArray(missing)
        << "}";
    return output.str();
}

std::vector<std::string> RuntimeMissingComponents(
    const std::string& rootPath,
    bool nativeRuntimeLinked,
    const std::string& platform) {
    std::vector<std::string> missing;
    if (!ReadyManifestExists(JoinPath(rootPath, "Runtime/ONNXRuntime/manifest.json"), platform)) {
        AddUnique(missing, "onnxruntime");
    }
    if (!nativeRuntimeLinked || !ReadyManifestExists(JoinPath(rootPath, "Runtime/StyleBertVits2/manifest.json"), platform)) {
        AddUnique(missing, "style_bert_vits2_runtime");
    }
    if (!FileExists(JoinPath(rootPath, "Runtime/JapaneseBert/model_fp16.onnx"))) {
        AddUnique(missing, "japanese_bert_onnx");
    }
    if (!FileExists(JoinPath(rootPath, "Runtime/JapaneseBert/tokenizer.json"))) {
        AddUnique(missing, "japanese_bert_tokenizer");
    }
    if (!StyleBertRuntimeHasJapaneseTextFrontend()
        || !ReadyManifestExists(JoinPath(rootPath, "Runtime/JapaneseTextFrontend/manifest.json"), platform)) {
        AddUnique(missing, "japanese_text_frontend");
    }
    return missing;
}

}  // namespace

std::string GetStatusJson(const std::string& requestJson, bool nativeRuntimeLinked) {
    const std::string rootPath = ExtractString(requestJson, "root_path");
    const std::string platform = ExtractString(requestJson, "platform");
    if (rootPath.empty()) {
        return ErrorJson("invalid_request", "Aivis native status request is missing root_path.");
    }

    const std::string catalogPath = JoinPath(rootPath, "aivis_voices.json");
    std::vector<std::string> missing;
    if (!DirectoryExists(rootPath)) {
        AddUnique(missing, "Aivis");
    }
    if (!FileExists(catalogPath)) {
        AddUnique(missing, "aivis_voices.json");
    }

    const bool modelsReady = DirectoryExists(rootPath) && FileExists(catalogPath);
    for (const auto& component : RuntimeMissingComponents(rootPath, nativeRuntimeLinked, platform)) {
        AddUnique(missing, component);
    }

    const bool textFrontendLinked = StyleBertRuntimeHasJapaneseTextFrontend();
    const bool runtimeReady = modelsReady && nativeRuntimeLinked && textFrontendLinked && missing.empty();
    std::ostringstream output;
    output
        << "{\"ok\":true"
        << ",\"native_runtime_linked\":" << (nativeRuntimeLinked ? "true" : "false")
        << ",\"text_frontend_linked\":" << (textFrontendLinked ? "true" : "false")
        << ",\"runtime_ready\":" << (runtimeReady ? "true" : "false")
        << ",\"models_ready\":" << (modelsReady ? "true" : "false")
        << ",\"root_path\":" << JsonString(rootPath)
        << ",\"catalog_path\":" << JsonString(catalogPath)
        << ",\"missing_components\":" << JsonArray(missing)
        << "}";
    return output.str();
}

std::string SynthesizeJson(const std::string& requestJson) {
    const std::string text = ExtractString(requestJson, "text");
    if (text.empty()) {
        return ErrorJson("invalid_request", "Aivis text is empty.");
    }

    const std::string modelPath = ExtractString(requestJson, "model_path");
    const std::string hyperParametersPath = ExtractString(requestJson, "hyper_parameters_path");
    const std::string styleVectorsPath = ExtractString(requestJson, "style_vectors_path");
    const std::string bertModelPath = ExtractString(requestJson, "bert_model_path");
    const std::string bertTokenizerPath = ExtractString(requestJson, "bert_tokenizer_path");
    const std::string bertVocabPath = ExtractString(requestJson, "bert_vocab_path");
    const std::string openJtalkDictPath = ExtractString(requestJson, "open_jtalk_dict_path");
    const std::string voicevoxModelPath = ExtractString(requestJson, "voicevox_model_path");
    if (!FileExists(modelPath)) {
        return ErrorJson("model_missing", "Aivis model was not found: " + modelPath);
    }
    if (!FileExists(hyperParametersPath)) {
        return ErrorJson("metadata_missing", "Aivis hyper parameters were not found: " + hyperParametersPath);
    }
    if (!FileExists(styleVectorsPath)) {
        return ErrorJson("style_vectors_missing", "Aivis style vectors were not found: " + styleVectorsPath);
    }
    if (!FileExists(bertModelPath)) {
        return ErrorJson("bert_model_missing", "Japanese BERT ONNX model was not found: " + bertModelPath, {"japanese_bert_onnx"});
    }
    if (!FileExists(bertTokenizerPath)) {
        return ErrorJson("bert_tokenizer_missing", "Japanese BERT tokenizer was not found: " + bertTokenizerPath, {"japanese_bert_tokenizer"});
    }
    if (!FileExists(bertVocabPath)) {
        return ErrorJson("bert_vocab_missing", "Japanese BERT vocabulary was not found: " + bertVocabPath, {"japanese_bert_tokenizer"});
    }
    if (!DirectoryExists(openJtalkDictPath)) {
        return ErrorJson("open_jtalk_dict_missing", "OpenJTalk dictionary was not found: " + openJtalkDictPath, {"japanese_text_frontend"});
    }
    if (!FileExists(voicevoxModelPath)) {
        return ErrorJson("voicevox_model_missing", "VOICEVOX helper model was not found: " + voicevoxModelPath, {"japanese_text_frontend"});
    }

    return SynthesizeStyleBertJson(requestJson);
}

} }  // namespace yui::aivis
