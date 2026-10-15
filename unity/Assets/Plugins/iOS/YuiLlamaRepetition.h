#pragma once
#include <algorithm>
#include <string>
namespace yui {
// Bounded suffix inspection. Detect sustained loops rather than ordinary
// repeated words; do not treat whitespace/Markdown separators as prose.
inline bool hasRepeatedTail(const std::string &text) {
    if(text.size()<144)return false;
    const size_t end=text.size(), maximum=std::min<size_t>(256,end/6);
    for(size_t width=24;width<=maximum;++width) {
        bool same=true;
        for(size_t repeat=2;repeat<=6;++repeat)
            if(text.compare(end-width,width,text,end-width*repeat,width)!=0){same=false;break;}
        if(!same)continue;
        for(size_t i=end-width;i<end;++i) {
            unsigned char c=text[i];
            if((c>='A'&&c<='Z')||(c>='a'&&c<='z')||(c>='0'&&c<='9')||c>=128)return true;
        }
    }
    return false;
}
}
