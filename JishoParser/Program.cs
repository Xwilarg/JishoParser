using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace JishoParser
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter thing");
            string str = Console.ReadLine();
            if (str == "1")
            {
                if (!File.Exists("kanjis.txt"))
                    throw new FileNotFoundException();
                Directory.CreateDirectory("img");
                var content = File.ReadAllText("kanjis.txt").ToCharArray();
                List<KanjiInfo> kanjis = new List<KanjiInfo>();
                using (HttpClient hc = new HttpClient())
                {
                    foreach (var elem in content)
                    {
                        Console.Write("\rParsing " + elem);
                        string html = hc.GetStringAsync("https://jisho.org/search/" + elem + "%20%23kanji").GetAwaiter().GetResult();
                        var kunyomi = !html.Contains("<dt>Kun:") ? new string[0] : Regex.Matches(html.Split(new[] { "<dt>Kun:" }, StringSplitOptions.None)[1].Split(new string[] { "</dd>" }, StringSplitOptions.None)[0], "<a[^>]+>([^<]+)<\\/a>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
                        kanjis.Add(new KanjiInfo
                        {
                            kanji = elem + (kunyomi.Length > 1 && kunyomi[0].Contains(".") ? kunyomi[0].Split('.')[1] : ""),
                            meaning = Regex.Match(html, "<div class=\"kanji-details__main-meanings\">([^<]+)<\\/div>").Groups[1].Value.Trim().Split(", "),
                            onyomi = !html.Contains("<dt>On:") ? new string[0] : Regex.Matches(html.Split(new[] { "<dt>On:" }, StringSplitOptions.None)[1].Split(new[] { "</dd>" }, StringSplitOptions.None)[0], "<a[^>]+>([^<]+)<\\/a>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray(),
                            kunyomi = kunyomi,
                            imageId = ((int)elem).ToString()
                        });
                        File.WriteAllBytes("img/" + ((int)elem).ToString() + ".png", hc.GetByteArrayAsync("http://classic.jisho.org/static/images/stroke_diagrams/" + (int)elem + "_frames.png").GetAwaiter().GetResult());
                    }
                }
                File.WriteAllText("result.txt", JsonConvert.SerializeObject(kanjis));
            }
            else if (str == "2")
            {
                Console.WriteLine("Enter jlpt level");
                List<WordInfo> words = new List<WordInfo>();
                string lvl = Console.ReadLine();
                int i = 1;
                while (true)
                {
                    using (HttpClient hc = new HttpClient())
                    {
                        string html = hc.GetStringAsync("https://jisho.org/search/%23jlpt-n" + lvl + "?page=" + i).GetAwaiter().GetResult();
                        var coll = Regex.Matches(html, "<span class=\"text\">([^<]+)<\\/span>");
                        if (coll.Count == 0)
                            break;
                        foreach (var elem in coll.Cast<Match>().Select(x => x.Groups[1].Value.Trim()))
                        {
                            Console.Write("\rParsing " + elem);
                            dynamic json = JsonConvert.DeserializeObject(hc.GetStringAsync("https://jisho.org/api/v1/search/words?keyword=" + elem).GetAwaiter().GetResult());
                            dynamic j = null;
                            foreach (var tmp in json.data)
                            {
                                if (((JArray)tmp.jlpt).ToObject<string[]>().Contains("jlpt-n" + lvl))
                                {
                                    j = tmp;
                                    break;
                                }
                            }
                            if (j == null)
                            {
                                Console.WriteLine("Not found: " + elem);
                                continue;
                            }
                            List<string> meaning = new List<string>();
                            string reading = null;
                            foreach (var m in j.senses)
                            {
                                meaning.AddRange(((JArray)m.english_definitions).ToObject<string[]>());
                            }
                            foreach (var r in j.japanese)
                            {
                                if (r.word == elem)
                                {
                                    reading = r.reading;
                                    break;
                                }
                            }
                            if (reading == null)
                            {
                                Console.WriteLine("Invalid reading: " + elem);
                                continue;
                            }
                            string slug = j.slug;
                            for (int I = 0; I < 10; I++)
                            {
                                slug = slug.Replace("-" + I, "");
                            }
                            words.Add(new WordInfo
                            {
                                word = slug,
                                meaning = meaning.Select(x =>
                                {
                                    var toRemove = Regex.Replace(x, "[^(]+(\\(.+\\))", "$1");
                                    var final = x.Replace(toRemove, "").Trim();
                                    return final == "" ? x : final;
                                }).ToArray(),
                                reading = reading
                            });
                        }
                    }
                    i++;
                }
                File.WriteAllText("result.txt", JsonConvert.SerializeObject(words));
            }
            else
                throw new ArgumentException();
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        public class KanjiInfo
        {
            public string kanji;
            public string[] meaning;
            public string[] onyomi;
            public string[] kunyomi;
            public string imageId;
        }

        public class WordInfo
        {
            public string word;
            public string[] meaning;
            public string reading;
        }
    }
}
