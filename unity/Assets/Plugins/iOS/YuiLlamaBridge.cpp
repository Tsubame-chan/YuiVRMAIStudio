// Yui's C ABI adapter for the pinned MIT llama.cpp runtime. No network or model conversion.
#include <llama.h>
#include "YuiLlamaRepetition.h"
#include "YuiLlamaChatTemplate.h"
#ifdef __APPLE__
#include <TargetConditionals.h>
#if TARGET_OS_IOS
#include <sys/stat.h>
#endif
#endif
#include <atomic>
#include <chrono>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>
#include <stdexcept>
#include <algorithm>
#include <thread>
#ifdef __APPLE__
// Managed worker threads do not supply an Objective-C autorelease pool.
// Drain temporary Metal objects after each request, including failed loads.
extern "C" void *objc_autoreleasePoolPush(void);
extern "C" void objc_autoreleasePoolPop(void *);
#endif

namespace {
#ifdef __APPLE__
struct AppleAutoreleasePool {
    void *value=objc_autoreleasePoolPush();
    ~AppleAutoreleasePool(){objc_autoreleasePoolPop(value);}
};
#endif
struct Request { std::atomic<bool> cancelled{false}; std::chrono::steady_clock::time_point deadline; };
std::mutex statesMutex, engineMutex;
std::unordered_map<std::string, std::shared_ptr<Request>> states;
std::once_flag initialized;
std::shared_ptr<Request> state(const char *id) {
    std::lock_guard<std::mutex> lock(statesMutex);
    auto &s = states[id ? id : ""]; if (!s) s = std::make_shared<Request>(); return s;
}
bool stopped(void *data) { auto *s = static_cast<Request *>(data); return s->cancelled || std::chrono::steady_clock::now() >= s->deadline; }
bool loading(float, void *data) { return !stopped(data); }
void check(Request *s) { if (stopped(s)) throw std::runtime_error(s->cancelled ? "cancelled" : "Model request timed out."); }
char *copy(const std::string &s) { auto *p = static_cast<char *>(std::malloc(s.size()+1)); if (p) std::memcpy(p,s.c_str(),s.size()+1); return p; }
}
extern "C" {
#ifdef _WIN32
int YuiLlama_ABIVersion() { return 1; }
#endif
void YuiLlama_Cancel(const char *id) { state(id)->cancelled = true; }
void YuiLlama_CancelAll() { std::lock_guard<std::mutex> lock(statesMutex); for(auto &item:states) item.second->cancelled=true; }
void YuiLlama_Forget(const char *id) { std::lock_guard<std::mutex> lock(statesMutex); states.erase(id ? id : ""); }
void YuiLlama_Free(void *p) { std::free(p); }
char *YuiLlama_Generate(const char *id, const char *path, const char **roles, const char **contents, int count,
    int contextTokens, int outputTokens, float temperature, int topK, float topP, int timeout, char **error) {
#ifdef __APPLE__
    AppleAutoreleasePool autoreleasePool;
#endif
    if (error) *error = nullptr;
    auto s = state(id); s->deadline = std::chrono::steady_clock::now() + std::chrono::seconds(std::clamp(timeout,30,600));
    try {
        if (!path || !roles || !contents || count < 1 || count > 128) throw std::runtime_error("Invalid chat request.");
        // Cancels while queued too. One native model per process prevents GPU/RAM overlap.
        std::unique_lock<std::mutex> lock(engineMutex,std::defer_lock);
        while (!lock.try_lock()) { check(s.get()); std::this_thread::sleep_for(std::chrono::milliseconds(20)); }
        check(s.get());
#if defined(__APPLE__) && TARGET_OS_IOS
        struct stat fileInfo;
        if (stat(path,&fileInfo)!=0) throw std::runtime_error("The GGUF file is missing.");
        // File size is not a reliable estimate of mmap/Metal residency. Optional
        // models are tried at the user's choice; allocation errors come from the runtime.
#endif
        std::call_once(initialized, [] { llama_backend_init(); });
        auto mp = llama_model_default_params(); mp.n_gpu_layers = -1; mp.load_mode = LLAMA_LOAD_MODE_MMAP;
        mp.progress_callback = loading; mp.progress_callback_user_data = s.get();
        std::unique_ptr<llama_model,decltype(&llama_model_free)> model(llama_model_load_from_file(path,mp),llama_model_free);
        check(s.get()); if (!model) throw std::runtime_error("GGUF could not be loaded. Check architecture, quantization, file integrity and available memory.");
        const auto *vocab = llama_model_get_vocab(model.get());
        auto cp = llama_context_default_params(); cp.n_ctx = std::clamp(contextTokens,4096,8192); cp.n_batch = 256; cp.n_ubatch = 128;
        cp.n_threads = cp.n_threads_batch = std::max(1u,std::min(4u,std::thread::hardware_concurrency()));
        cp.abort_callback = stopped; cp.abort_callback_data = s.get();
        std::unique_ptr<llama_context,decltype(&llama_free)> ctx(llama_init_from_model(model.get(),cp),llama_free);
        check(s.get()); if (!ctx) throw std::runtime_error("Not enough memory for this model and context. Reduce context or choose a smaller quantized model.");
        const char *tmpl = llama_model_chat_template(model.get(),nullptr);
        if (!tmpl) throw std::runtime_error("This GGUF has no chat template. Choose an instruction/chat model with an embedded template.");
        std::vector<llama_chat_message> messages;
        for (int i=0;i<count;++i) messages.push_back({roles[i],contents[i]});
        std::vector<llama_token> tokens;
        bool prefilledThinking=false;
        outputTokens = std::clamp(outputTokens,1,4096);
        while (true) {
            check(s.get());
            std::string prompt;
            try {
                auto bos=llama_vocab_bos(vocab);auto eos=llama_vocab_eos(vocab);
                auto bosText=bos>=0?llama_vocab_get_text(vocab,bos):"";auto eosText=eos>=0?llama_vocab_get_text(vocab,eos):"";
                auto noGeneration=yui::renderChatTemplate(tmpl,messages,bosText,eosText,[&]{check(s.get());},false);
                prompt=yui::renderChatTemplate(tmpl,messages,bosText,eosText,[&]{check(s.get());});
                size_t shared=0;while(shared<std::min(prompt.size(),noGeneration.size()) && prompt[shared]==noGeneration[shared])++shared;
                auto prefix=prompt.substr(shared);auto open=prefix.rfind("<think>");auto closed=prefix.rfind("</think>");
                prefilledThinking=open!=std::string::npos && (closed==std::string::npos || open>closed);
            } catch(const std::exception &) {check(s.get());throw std::runtime_error("This GGUF's chat template could not be rendered by this runtime.");}
            int size=prompt.size();
            auto bos=llama_vocab_bos(vocab);const char *bosText=bos>=0?llama_vocab_get_text(vocab,bos):nullptr;
            bool addSpecial=!(bosText && *bosText && prompt.compare(0,std::strlen(bosText),bosText)==0);
            int n = -llama_tokenize(vocab,prompt.data(),size,nullptr,0,addSpecial,true);
            if (n < 1) throw std::runtime_error("Could not tokenize the chat input.");
            tokens.resize(n); if (llama_tokenize(vocab,prompt.data(),size,tokens.data(),tokens.size(),addSpecial,true)<0) throw std::runtime_error("Tokenization failed.");
            if (tokens.size()+outputTokens <= llama_n_ctx(ctx.get())) break;
            // Drop oldest complete turns, preserving personality and the current input.
            if (messages.size() <= 3) throw std::runtime_error("Input exceeds this model's context. Shorten the message or reduce output limits.");
            auto start = std::strcmp(messages.front().role,"system")==0 ? 1 : 0;
            messages.erase(messages.begin()+start,messages.begin()+start+2);
        }
        for (size_t offset=0; offset<tokens.size();offset+=cp.n_batch) {
            check(s.get()); auto batch = llama_batch_get_one(tokens.data()+offset,std::min<size_t>(cp.n_batch,tokens.size()-offset));
            if (llama_decode(ctx.get(),batch)!=0) { check(s.get()); throw std::runtime_error("GGUF prompt evaluation failed."); }
        }
        std::unique_ptr<llama_sampler,decltype(&llama_sampler_free)> sampler(llama_sampler_chain_init(llama_sampler_chain_default_params()),llama_sampler_free);
        // Penalize repeated candidates before top-k/top-p truncate their alternatives.
        llama_sampler_chain_add(sampler.get(),llama_sampler_init_penalties(llama_vocab_n_tokens(vocab),64,1.1f,0.f,0.f));
        if (temperature <= 0) {
            llama_sampler_chain_add(sampler.get(),llama_sampler_init_greedy());
        }
        else {
            llama_sampler_chain_add(sampler.get(),llama_sampler_init_top_k(std::clamp(topK,1,100)));
            llama_sampler_chain_add(sampler.get(),llama_sampler_init_top_p(std::clamp(topP,.1f,1.f),1));
            llama_sampler_chain_add(sampler.get(),llama_sampler_init_temp(std::clamp(temperature,0.f,1.5f)));
            llama_sampler_chain_add(sampler.get(),llama_sampler_init_dist(LLAMA_DEFAULT_SEED));
        }
        std::string output=prefilledThinking?"<think>":"";
        for (int i=0;i<outputTokens;++i) {
            check(s.get()); auto token = llama_sampler_sample(sampler.get(),ctx.get(),-1);
            if (llama_vocab_is_eog(vocab,token)) break;
            std::vector<char> piece(128); int n = llama_token_to_piece(vocab,token,piece.data(),piece.size(),0,true);
            if (n<0) { piece.resize(-n); n=llama_token_to_piece(vocab,token,piece.data(),piece.size(),0,true); }
            if (n>0) output.append(piece.data(),n);
            if(yui::hasRepeatedTail(output))throw std::runtime_error("Repeated output detected. Try another model or reduce the output limit.");
            if (i+1 < outputTokens) { auto batch = llama_batch_get_one(&token,1); if (llama_decode(ctx.get(),batch)!=0) { check(s.get()); throw std::runtime_error("GGUF generation failed."); } }
        }
        check(s.get()); return copy(output);
    } catch (const std::exception &e) { if(error) *error = copy(e.what()); return nullptr; }
}
}
