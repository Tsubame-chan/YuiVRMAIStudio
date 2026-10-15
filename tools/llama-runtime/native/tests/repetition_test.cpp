#include "../../../../unity/Assets/Plugins/iOS/YuiLlamaRepetition.h"
#include <cassert>
#include <iostream>
std::string repeated(const std::string &s,int n){std::string out;while(n--)out+=s;return out;}
int main(){
 assert(yui::hasRepeatedTail(repeated("I am from...",20)));
 assert(yui::hasRepeatedTail(repeated("This is a long English sentence that repeats without providing an answer. ",8)));
 assert(yui::hasRepeatedTail(repeated("同じ文章を繰り返しています。",12)));
 assert(!yui::hasRepeatedTail("Hello, I am Yui. How can I help you today?"));
 assert(!yui::hasRepeatedTail(repeated("The chorus comes back. ",3)));
 assert(!yui::hasRepeatedTail(repeated("---   ",100)));
 std::cout<<"6 repetition guard cases passed\n";
}
