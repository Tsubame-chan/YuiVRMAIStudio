#pragma once
#include <llama.h>
#include "YuiLlamaJinja/jinja/yui-jinja-parser.h"
#include <functional>
#include <stdexcept>
#include <cstring>
namespace yui {
inline std::string renderChatTemplate(const char *source,const std::vector<llama_chat_message> &messages,
    const std::string &bos,const std::string &eos,const std::function<void()> &check,bool addGenerationPrompt=true) {
    if(!source || std::strlen(source)>512*1024)throw std::runtime_error("Missing or excessively large GGUF chat template.");
    std::string text(source);
    if(text.find("{{")==std::string::npos && text.find("{%") ==std::string::npos) {
        int size=llama_chat_apply_template(source,messages.data(),messages.size(),addGenerationPrompt,nullptr,0);
        if(size<0 || size>4*1024*1024)throw std::runtime_error("Unsupported GGUF chat template alias.");
        std::vector<char> buffer(size+1);llama_chat_apply_template(source,messages.data(),messages.size(),addGenerationPrompt,buffer.data(),buffer.size());
        return std::string(buffer.data(),size);
    }
    jinja::lexer lexer;auto program=jinja::parse_from_tokens(lexer.tokenize(text));jinja::context context(text);
    size_t steps=0;context.on_step=[&]{if(++steps>1000000)throw std::runtime_error("GGUF chat template exceeded its execution budget.");if((steps%256)==0)check();};
    auto list=jinja::mk_val<jinja::value_array>();
    for(const auto &message:messages) {
        auto item=jinja::mk_val<jinja::value_object>();item->insert("role",jinja::mk_val<jinja::value_string>(message.role));
        auto content=jinja::mk_val<jinja::value_string>(message.content);content->mark_input();item->insert("content",content);list->push_back(item);
    }
    context.set_val("messages",list);context.set_val("bos_token",jinja::mk_val<jinja::value_string>(bos));context.set_val("eos_token",jinja::mk_val<jinja::value_string>(eos));
    context.set_val("add_generation_prompt",jinja::mk_val<jinja::value_bool>(addGenerationPrompt));
    context.set_val("enable_thinking",jinja::mk_val<jinja::value_bool>(false));
    context.set_val("tools",jinja::mk_val<jinja::value_array>());
    check();jinja::runtime runtime(context);auto value=runtime.execute(program);check();
    auto prompt=jinja::runtime::gather_string_parts(value)->as_string().str();
    if(prompt.empty() || prompt.size()>4*1024*1024)throw std::runtime_error("GGUF chat template produced an empty or excessively large prompt.");
    return prompt;
}
}
