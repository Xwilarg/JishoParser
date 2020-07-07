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
                    kanjis.Add(new KanjiInfo
                    {
                        kanji = elem,
                        meaning = Regex.Match(html, "<div class=\"kanji-details__main-meanings\">([^<]+)<\\/div>").Groups[1].Value.Trim().Split(", "),
                        onyomi = !html.Contains("<dt>On:") ? new string[0] : Regex.Matches(html.Split(new[] { "<dt>On:" }, StringSplitOptions.None)[1].Split(new[] { "</dd>" }, StringSplitOptions.None)[0], "<a[^>]+>([^<]+)<\\/a>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray(),
                        kunyomi = !html.Contains("<dt>Kun:") ? new string[0] : Regex.Matches(html.Split(new[] { "<dt>Kun:" }, StringSplitOptions.None)[1].Split(new string[] { "</dd>" }, StringSplitOptions.None)[0], "<a[^>]+>([^<]+)<\\/a>").Cast<Match>().Select(x => x.Groups[1].Value).ToArray(),
                        imageId = ((int)elem).ToString()
                    });
                    File.WriteAllBytes("img/" + ((int)elem).ToString() + ".png", hc.GetByteArrayAsync("http://classic.jisho.org/static/images/stroke_diagrams/" + (int)elem + "_frames.png").GetAwaiter().GetResult());
                }
            }
            File.WriteAllText("result.txt", JsonConvert.SerializeObject(kanjis));
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        public class KanjiInfo
        {
            public char kanji;
            public string[] meaning;
            public string[] onyomi;
            public string[] kunyomi;
            public string imageId;
        }
    }
}
