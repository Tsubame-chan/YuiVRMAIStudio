#include "../../../../unity/Assets/Plugins/iOS/YuiLlamaChatTemplate.h"
#include <cassert>
#include <iostream>
int main(){
 std::vector<llama_chat_message> messages={{"system","You are Yui."},{"user","こんにちは <think>literal"}};
 const char *t="{{ bos_token }}{% for message in messages %}[{{ message.role }}]{{ message.content }}{% endfor %}{% if add_generation_prompt %}[assistant]{% if enable_thinking %}<think>{% else %}final:{% endif %}{% endif %}";
 auto yes=yui::renderChatTemplate(t,messages,"<bos>","<eos>",[]{});
 auto no=yui::renderChatTemplate(t,messages,"<bos>","<eos>",[]{},false);
 assert(yes=="<bos>[system]You are Yui.[user]こんにちは <think>literal[assistant]final:");
 assert(yes.substr(no.size())=="[assistant]final:");
 bool cancelled=false;
 try {
  jinja::lexer lexer;auto program=jinja::parse_from_tokens(lexer.tokenize("{% for x in range(10000) %}{{x}}{% endfor %}"));
  jinja::context context;size_t steps=0;context.on_step=[&]{if(++steps==64)throw std::runtime_error("cancelled");};
  jinja::runtime runtime(context);runtime.execute(program);
 }
 catch(const std::exception &){cancelled=true;}assert(cancelled);
 bool rejected=false;
 try {yui::renderChatTemplate("{% if %}",messages,"","",[]{});}catch(const std::exception &){rejected=true;}assert(rejected);
 bool alias=false;try {yui::renderChatTemplate("unknown_alias_123",messages,"","",[]{});}catch(const std::exception &){alias=true;}assert(alias);
 std::cout<<"5 template cases passed: roles/UTF8, generation prefix, cancellation, malformed template, unsupported alias\n";
}
