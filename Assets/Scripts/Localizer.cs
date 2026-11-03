using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// Display-only localization. Never translate or mutate the network's canonical messages/IDs.
public static class Localizer
{
    public static GameLocalization Catalog { get; private set; }
    public static GameLanguage Language { get; private set; } = GameLanguage.English;
    public static int Revision { get; private set; }
    public static bool Vietnamese => Language == GameLanguage.Vietnamese;
    private static readonly Dictionary<string,TranslationEntry> lookup = new Dictionary<string,TranslationEntry>();
    private static readonly Dictionary<string,string> cache = new Dictionary<string,string>();
    private class Pattern { public Regex regex; public TranslationEntry entry; public int[] order; }
    private static readonly List<Pattern> patterns = new List<Pattern>();
    private static string PreferenceKey => "thereasm.language." + (Catalog != null ? Catalog.defaultLanguage.ToString() : "English");
    public static void Invalidate(GameLocalization catalog){if(Catalog==catalog){Catalog=null;cache.Clear();}}

    public static void Initialize(GameLocalization catalog, GameLanguage preview)
    {
        if (catalog == null) return;
        bool changed = Catalog != catalog;
        Catalog = catalog;
        if (changed)
        {
            Revision++;
            lookup.Clear(); patterns.Clear(); cache.Clear();
            if (catalog.entries != null) foreach (var entry in catalog.entries)
                foreach (string text in new[]{entry.source,entry.english,entry.vietnamese})
                {
                    if (string.IsNullOrEmpty(text)) continue;
                    lookup[text] = entry;
                    if (Regex.IsMatch(text,@"\{\d+\}")) AddPattern(text,entry);
                }
            patterns.Sort((a,b)=>b.regex.ToString().Length.CompareTo(a.regex.ToString().Length));
        }
        if (!Application.isPlaying) SetLanguage(preview,false);
        else if (changed)
        {
            int preference=PlayerPrefs.GetInt(PreferenceKey,(int)catalog.defaultLanguage);
            var language=preference==1?GameLanguage.Vietnamese:GameLanguage.English;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-thereasm-language");
            if(index>=0&&index+1<args.Length)language=args[index+1]=="vi"?GameLanguage.Vietnamese:GameLanguage.English;
            SetLanguage(language,false);
        }
    }
    private static void AddPattern(string text,TranslationEntry entry)
    {
        var order=new List<int>();var pattern=new StringBuilder("\\A");int start=0;
        foreach(Match match in Regex.Matches(text,@"\{(\d+)\}"))
        {
            pattern.Append(Regex.Escape(text.Substring(start,match.Index-start)));
            order.Add(int.Parse(match.Groups[1].Value));pattern.Append("([\\s\\S]+?)");start=match.Index+match.Length;
        }
        pattern.Append(Regex.Escape(text.Substring(start))).Append("\\z");
        patterns.Add(new Pattern{regex=new Regex(pattern.ToString(),RegexOptions.CultureInvariant),entry=entry,order=order.ToArray()});
    }
    public static void SetLanguage(GameLanguage language,bool remember=true)
    {
        if(Language==language&&Revision>0)return;
        Language=language;Revision++;cache.Clear();
        if(remember&&Application.isPlaying){PlayerPrefs.SetInt(PreferenceKey,(int)language);PlayerPrefs.Save();}
    }
    public static string T(string source) => Translate(source,0);
    private static string Translate(string source,int depth)
    {
        if(string.IsNullOrEmpty(source)||Catalog==null)return source;
        if(cache.TryGetValue(source,out var value))return value;
        if(lookup.TryGetValue(source,out var entry))value=Vietnamese?entry.vietnamese:entry.english;
        else if(depth>0&&source.Contains(", "))
        {
            var pieces=source.Split(new[]{", "},StringSplitOptions.None);
            for(int i=0;i<pieces.Length;i++)pieces[i]=Translate(pieces[i],depth+1);
            value=string.Join(", ",pieces);
        }
        else if(depth<3)
        {
            foreach(var pattern in patterns)
            {
                var match=pattern.regex.Match(source);if(!match.Success)continue;
                var format=Vietnamese?pattern.entry.vietnamese:pattern.entry.english;
                int count=0;foreach(int number in pattern.order)count=Math.Max(count,number+1);
                var args=new object[count];for(int i=0;i<pattern.order.Length;i++)args[pattern.order[i]]=Translate(match.Groups[i+1].Value,depth+1);
                try { value=string.Format(format,args); } catch(FormatException) { value=source; }
                break;
            }
        }
        if(value==null&&source.Contains("\n"))
        {
            var lines=source.Split('\n');for(int i=0;i<lines.Length;i++)lines[i]=Translate(lines[i],depth+1);
            value=string.Join("\n",lines);
        }
        if(string.IsNullOrEmpty(value))value=source;
        if(depth==0){if(cache.Count>1024)cache.Clear();cache[source]=value;}
        return value;
    }
}
