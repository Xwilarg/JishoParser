using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
                Console.WriteLine("Enter jlpt level");
                List<WordInfo> words = new List<WordInfo>();
                string lvl = Console.ReadLine();
                int i = 1;
                Directory.CreateDirectory("img");
                var content = File.ReadAllText("kanjis.txt").ToCharArray();
                List<KanjiInfo> kanjis = new List<KanjiInfo>();
                using (HttpClient hc = new HttpClient())
                {
                    while (true)
                    {
                        string html;
                        while (true)
                        {
                            try
                            {
                                html = hc.GetStringAsync("https://jisho.org/search/%23jlpt-n" + lvl + "%23kanji?page=" + i).GetAwaiter().GetResult();
                                break;
                            }
                            catch (HttpRequestException)
                            { }
                            catch (TaskCanceledException)
                            { }
                        }
                        var coll = Regex.Matches(html, "<a href=\"\\/\\/jisho\\.org\\/search\\/[^k]+kanji\">([^<])<\\/a>");
                        if (coll.Count == 0)
                            break;
                        foreach (var elem in coll.Cast<Match>().Select(x => x.Groups[1].Value[0]))
                        {
                            Console.Write("\rParsing " + elem);
                            html = hc.GetStringAsync("https://jisho.org/search/" + elem + "%20%23kanji").GetAwaiter().GetResult();
                            var kunyomi = !html.Contains("<dt>Kun:") ? new string[0] : Regex.Matches(html.Split(new[] { "<dt>Kun:" }, StringSplitOptions.None)[1].Split(new string[] { "</dd>" }, StringSplitOptions.None)[0], "<a[^>]+>([^<]+)<\\/a>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
                            kanjis.Add(new KanjiInfo
                            {
                                kanji = elem + (kunyomi.Length > 1 && kunyomi[0].Contains(".") ? kunyomi[0].Split('.')[1] : ""),
                                meaning = Regex.Match(html, "<div class=\"kanji-details__main-meanings\">([^<]+)<\\/div>").Groups[1].Value.Trim().Split(", "),
                                onyomi = !html.Contains("<dt>On:") ? new string[0] : Regex.Matches(html.Split(new[] { "<dt>On:" }, StringSplitOptions.None)[1].Split(new[] { "</dd>" }, StringSplitOptions.None)[0], "<a[^>]+>([^<]+)<\\/a>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray(),
                                kunyomi = kunyomi,
                                imageId = ((int)elem).ToString()
                            });
                            while (true)
                            {
                                try
                                {
                                    File.WriteAllBytes("img/" + ((int)elem).ToString() + ".png", hc.GetByteArrayAsync("http://classic.jisho.org/static/images/stroke_diagrams/" + (int)elem + "_frames.png").GetAwaiter().GetResult());
                                    break;
                                }
                                catch (HttpRequestException)
                                { }
                                catch (TaskCanceledException)
                                { }
                            }
                        }
                        i++;
                    }
                }
                File.WriteAllText("result.txt", JsonConvert.SerializeObject(kanjis));
            }
            else if (str == "2")
            {
                Console.WriteLine("Enter jlpt level");
                List<WordInfo> words = new List<WordInfo>();
                string lvl = Console.ReadLine();
                Console.WriteLine("Only get nouns: [Y/N]");
                var output = Console.ReadLine();
                bool onlyNouns = output == "Y" || output == "y";
                int i = 1;
                while (true)
                {
                    using (HttpClient hc = new HttpClient())
                    {
                        string html;
                        while (true)
                        {
                            try
                            {
                                html = hc.GetStringAsync("https://jisho.org/search/%23jlpt-n" + lvl + "?page=" + i).GetAwaiter().GetResult();
                                break;
                            }
                            catch (HttpRequestException)
                            { }
                            catch (TaskCanceledException)
                            { }
                        }
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
                                if (!onlyNouns || ((JArray)m.parts_of_speech).ToObject<string[]>().Contains("Noun"))
                                    meaning.AddRange(((JArray)m.english_definitions).ToObject<string[]>());
                            }
                            if (meaning.Count == 0)
                                continue;
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
                Console.WriteLine("Output format: 1 - JSON, 2 - Text");
                if (Console.ReadLine() == "1")
                    File.WriteAllText("result.txt", JsonConvert.SerializeObject(words));
                else
                    File.WriteAllLines("result.txt", words.Select(x => x.reading + " " + string.Join(",", x.meaning.Select(x => "\"" + x + "\""))));
            }
            else if (str == "3")
            {
                var lines = File.ReadAllLines("sentences.txt");
                var final = lines.Take(1000).ToArray();
                List<SentenceInfo> sentences = new List<SentenceInfo>();
                for (int i = 0; i < final.Length; i += 2)
                {
                    string[] l = final[i].Substring(3).Split('	');
                    string help = final[i + 1].Substring(3);
                    List <SentenceWordInfo> words = new List<SentenceWordInfo>();
                    string[] particles = new[] { "の", "を", "に", "で", "と", "も", "か", "は", "が", "から", "まで", "へ" };
                    char[] punctuation = new[] { '。', '、', '？', '！', '「', '」', '０', '１', '２', '３', '９' };
                    string curr = l[0];
                    int particleCount = 0;
                    Console.Write("\rParsing " + curr);
                    foreach (string s in help.Split(' ').Select(x => x.Replace("~", "")))
                    {
                        string p = "";
                        while (curr.Length > 0 && punctuation.Contains(curr[0]))
                        {
                            p += curr[0];
                            curr = curr.Substring(1);
                        }
                        if (p != "")
                        {
                            words.Add(new SentenceWordInfo
                            {
                                word = p,
                                isParticle = false
                            });
                        }
                        if (curr.Length == 0)
                            break;
                        bool isParticle;
                        string word;
                        if (particles.Contains(s))
                        {
                            word = s;
                            isParticle = true;
                            particleCount++;
                        }
                        else
                        {
                            isParticle = false;
                            var m = Regex.Match(s, "{([^}]+)}");
                            if (m.Success)
                            {
                                word = m.Groups[1].Value;
                            }
                            else
                            {
                                word = s;
                                m = Regex.Match(word, "\\([^}]+\\)");
                                if (m.Success) word = word.Replace(m.Value, "");
                                m = Regex.Match(word, "\\[[0-9]+\\]");
                                if (m.Success) word = word.Replace(m.Value, "");
                            }
                        }
                        words.Add(new SentenceWordInfo
                        {
                            word = word,
                            isParticle = isParticle
                        });
                        if (!curr.StartsWith(word))
                        {
                            Console.WriteLine("Error while parsing " + curr + ", can't find " + word);
                            goto next;
                        }
                        curr = curr.Substring(word.Length);
                    }
                    if (particleCount == 0)
                    {
                        Console.WriteLine("No particle found in " + l[0]);
                    }
                    else
                    {
                        sentences.Add(new SentenceInfo
                        {
                            meaning = l[1].Split('#')[0],
                            words = words.ToArray(),
                            particleCount = particleCount
                        });
                    }
                next:;
                }
                File.WriteAllText("result.txt", JsonConvert.SerializeObject(sentences));
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

        public class SentenceInfo
        {
            public SentenceWordInfo[] words;
            public string meaning;
            public int particleCount;
        }

        public class SentenceWordInfo
        {
            public string word;
            public bool isParticle; // 0: particle, 1: other
        }
    }
}
