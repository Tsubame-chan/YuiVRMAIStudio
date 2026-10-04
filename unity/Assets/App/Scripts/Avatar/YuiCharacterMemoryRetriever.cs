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
        private static DateTimeOffset RecordedTime(string value) => DateTimeOffset.TryParse(value,
            System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal,out var parsed) ? parsed.ToUniversalTime() : DateTimeOffset.MinValue;
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
            if(Regex.IsMatch(text,@"悲し|絶好調|調子|落ち込|気分|つら|辛|不安|嬉し|うれし|sad|happy|mood"))terms.Add("category:mood");
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
        private static bool HasRequiredLiteral(string query,string content)
        {
            var normalizedQuery=(query??"").Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            var normalizedContent=(content??"").Normalize(NormalizationForm.FormKC).ToLowerInvariant();
            // Character bigrams can otherwise mistake 1999 for 9999.
            var contentNumbers=new HashSet<string>(Regex.Matches(normalizedContent,@"(?<![a-z0-9])\d+(?![a-z0-9])").Cast<Match>().Select(m=>m.Value));
            foreach(Match number in Regex.Matches(normalizedQuery,@"(?<![a-z0-9])\d+(?![a-z0-9])"))
                if(!contentNumbers.Contains(number.Value))return false;
            // A bare name lookup must not turn Alice into Alicia through shared bigrams.
            if(Regex.IsMatch(normalizedQuery,@"^[a-z]{3,}$") &&
                !Regex.Matches(normalizedContent,@"[a-z]{3,}").Cast<Match>().Any(m=>m.Value==normalizedQuery))return false;
            return true;
        }
        public string Retrieve(IReadOnlyList<YuiCharacterMemoryStore.Entry> entries,string query,int maxChars)
        {
            maxChars=Math.Max(0,Math.Min(2400,maxChars));
            var terms=Terms(query);
            var present=new HashSet<string>(entries.Select(e=>e.Id));
            var ranked=entries.Where(e=>(e.SourceIds==null || e.SourceIds.Length==0 || e.SourceIds.All(present.Contains)) && HasRequiredLiteral(query,e.Content)).Select((entry,index)=>new { Entry=entry,Index=index,
                Score=Terms(entry.Content).Count(terms.Contains)*3+(entry.Pinned?12:0) }).ToList();
            var candidates=ranked.Where(e=>e.Score>0).ToList();
            // Empty-query prompts need a small general reminder. A specific unrelated
            // question must not receive an arbitrary recent personal fact instead.
            if(candidates.Count==0 && string.IsNullOrWhiteSpace(query))
                candidates=ranked.Where(e=>Terms(e.Entry.Content).Any(t=>t.StartsWith("category:",StringComparison.Ordinal))).OrderByDescending(e=>RecordedTime(e.Entry.CreatedUtc)).Take(2).ToList();
            var selected=candidates.OrderByDescending(e=>e.Score).ThenByDescending(e=>RecordedTime(e.Entry.CreatedUtc)).ThenByDescending(e=>e.Index).Take(5).ToList();
            var accepted=new List<Tuple<string,string>>();var used=0;
            foreach(var item in selected) {
                var label=item.Entry.SourceIds!=null && item.Entry.SourceIds.Length>0?"User-confirmed interpretation (sources: "+string.Join(",",item.Entry.SourceIds)+"): ":"Saved user-related note: ";
                var line="- "+item.Entry.CreatedUtc+" "+label+Excerpt(item.Entry.Content,terms)+"\n";
                if(used+line.Length>maxChars)continue;
                accepted.Add(Tuple.Create(item.Entry.CreatedUtc,line));used+=line.Length;
            }
            return string.Concat(accepted.OrderBy(e=>RecordedTime(e.Item1)).Select(e=>e.Item2));
        }
    }
}
