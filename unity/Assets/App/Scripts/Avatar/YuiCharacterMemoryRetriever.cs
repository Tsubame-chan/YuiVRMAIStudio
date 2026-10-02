using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace YuiPhysicalAI.Avatar
{
    // A vector/embedding retriever can replace this policy without changing storage,
    // character scoping, secret-mode write rules, or any AI provider adapter.
    public interface IYuiCharacterMemoryRetriever
    {
        string Retrieve(IReadOnlyList<YuiCharacterMemoryStore.Entry> entries,string query,int maxChars);
    }
    public sealed class YuiCharacterMemoryRetriever : IYuiCharacterMemoryRetriever
    {
        private static HashSet<string> Terms(string value)
        {
            var text=Regex.Replace((value??"").Normalize(NormalizationForm.FormKC).ToLowerInvariant(), @"\s+", "");
            var terms=new HashSet<string>();
            foreach(Match word in Regex.Matches(text,@"[a-z0-9]{2,}"))terms.Add(word.Value);
            for(var i=0;i+1<text.Length;i++)if(char.IsLetterOrDigit(text[i])&&char.IsLetterOrDigit(text[i+1]))terms.Add(text.Substring(i,2));
            if(Regex.IsMatch(text,@"好き|嫌い|苦手|好み|お気に入り|favorite|prefer|like|dislike|allerg"))terms.Add("category:preference");
            if(Regex.IsMatch(text,@"食|料理|カレー|肉|夕飯|昼飯|ご飯|献立|food|meal|curry|chicken|dinner|menu"))terms.Add("category:food");
            if(Regex.IsMatch(text,@"名前|呼ん|呼べ|name|call"))terms.Add("category:identity");
            if(Regex.IsMatch(text,@"約束|予定|promise|plan"))terms.Add("category:promise");
            if(Regex.IsMatch(text,@"恋人|彼女|彼氏|相棒|友達|relationship|girlfriend|boyfriend|companion"))terms.Add("category:relationship");
            return terms;
        }
        private static string Excerpt(string text,HashSet<string> query)
        {
            if(text.Length<=450)return text;
            // Overlap retains a fact spanning a chunk boundary; rank excerpts by relevance.
            var chunks=new List<string>();
            for(var i=0;i<text.Length;i+=300)chunks.Add(text.Substring(i,Math.Min(450,text.Length-i)));
            return chunks.OrderByDescending(c=>Terms(c).Count(query.Contains)).First()+"…";
        }
        public string Retrieve(IReadOnlyList<YuiCharacterMemoryStore.Entry> entries,string query,int maxChars)
        {
            maxChars=Math.Max(0,Math.Min(2400,maxChars));
            var terms=Terms(query);
            var ranked=entries.Select((entry,index)=>new { Entry=entry,Index=index,
                Score=Terms(entry.Content).Count(terms.Contains)*3+(entry.Pinned?12:0) }).ToList();
            var candidates=ranked.Where(e=>e.Score>0).ToList();
            if(candidates.Count==0)candidates=ranked.Where(e=>Terms(e.Entry.Content).Any(t=>t.StartsWith("category:",StringComparison.Ordinal))).OrderByDescending(e=>e.Entry.CreatedUtc).Take(2).ToList();
            var selected=candidates.OrderByDescending(e=>e.Score).ThenByDescending(e=>e.Entry.CreatedUtc).ThenByDescending(e=>e.Index).Take(5).ToList();
            var accepted=new List<Tuple<string,string>>();var used=0;
            foreach(var item in selected) {
                var line="- "+item.Entry.CreatedUtc+" User said: "+Excerpt(item.Entry.Content,terms)+"\n";
                if(used+line.Length>maxChars)continue;
                accepted.Add(Tuple.Create(item.Entry.CreatedUtc,line));used+=line.Length;
            }
            return string.Concat(accepted.OrderBy(e=>e.Item1).Select(e=>e.Item2));
        }
    }
}
