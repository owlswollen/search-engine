using System;
using Nuve.Tokenizers;
using Nuve.Lang;
using Nuve.Morphologic.Structure;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace SearchEngine
{
    class Search
    {
        public Search()
        {
            CalcCosineSim();
            Ranking();
        }

        private List<string> GetKeywords()
        {
            Console.WriteLine("Keywords:");
            string keywords = Console.ReadLine();
            keywords = keywords.ToLower() + " ";

            IList<string> tokens = StandartSplitter.Split(keywords);
            List<string> stems = new List<string>();
            Language tr = LanguageFactory.Create(LanguageType.Turkish);
            int ind = 0;
            foreach (string token in tokens)
            {
                if (token.Equals("dokuz"))
                {
                    ind = tokens.IndexOf(token);
                }
            }
            tokens[ind] = "eylül";

            foreach (string token in tokens)
            {
                IList<Word> solutions = tr.Analyze(token);
                if (solutions.Count > 0)
                {
                    if (!IsStopWord(solutions[solutions.Count - 1].GetStem().GetSurface()))
                    {
                        stems.Add(solutions[solutions.Count - 1].GetStem().GetSurface());
                    }
                }
            }
            return stems;
        }

        private bool IsStopWord(string stem)
        {
            FileStream filestream = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "StopWords.txt", FileMode.Open, FileAccess.Read);
            StreamReader reader = new StreamReader(filestream);
            string data = "";
            while (data != null)
            {
                data = reader.ReadLine();
                if (String.Compare(stem, data) == 0)
                    return true;
            }
            return false;
        }

        class CosineClass
        {
            public double cosine;
            public int pageIndex;
            public string uniName;
        }
        List<CosineClass> cosines = new List<CosineClass>();
        private void CalcCosineSim()
        {
            List<string> keywords = GetKeywords();
            foreach (string filePath in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "TF-IDFs"))
            {
                CosineClass cosineTemp = new CosineClass();
                List<double> tfIdfs = new List<double>();
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    foreach (string keyword in keywords)
                    {
                        string[] temp = line.Split(',');
                        if (temp[0].Equals(keyword))
                        {
                            tfIdfs.Add(Convert.ToDouble(temp[1]));
                        }
                    }
                }
                int k = keywords.Count - tfIdfs.Count;
                    for (int i = 0; i < k ;i++)
                        tfIdfs.Add(0);

                double cosine = 0;
                double carpim = 0;
                double buyukluk = 0;
                foreach (double tfIdf in tfIdfs)
                {
                    carpim += tfIdf;
                    buyukluk += Math.Pow(tfIdf, 2);
                }
                buyukluk = Math.Sqrt(buyukluk);
                cosine = carpim / (Math.Sqrt(tfIdfs.Count) * buyukluk);
                try
                {
                    cosineTemp.pageIndex = Convert.ToInt32(Path.GetFileName(filePath).Split('.')[0].Split('-')[1]);
                    cosineTemp.uniName = Path.GetFileName(filePath).Split('.')[0].Split('-')[0];
                    if (Double.IsNaN(cosine))
                        cosine = 0;
                    cosineTemp.cosine = cosine;
                    cosines.Add(cosineTemp);
                }
                catch { }
                
            }
        }
        List<string> links = new List<string>();
        private void Ranking()
        {
            var orderedCosines = cosines.OrderByDescending(x => x.cosine);
            int counter = 1;
            foreach(CosineClass item in orderedCosines)
            {
                if (item.cosine > 0)
                {
                    string link = "";
                    using (StreamReader sr = File.OpenText(AppDomain.CurrentDomain.BaseDirectory + item.uniName + ".txt"))
                    {
                        string line;
                        int lineCount = 0;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (lineCount == item.pageIndex)
                            {
                                link = line;
                                links.Add(link);
                                break;
                            }
                            lineCount++;
                        }
                    }
                    Console.WriteLine(counter + ") " + link + "\tcosine sim: " + item.cosine);
                    counter++;
                    if (counter == 101)
                        break;
                }
            }
            Menu();
        }

        private void Menu()
        {
            int linkNum;
            do
            {
                linkNum = 0;

                Console.WriteLine("Link Number (0 for new search): ");
                linkNum = Convert.ToInt32(Console.ReadLine()) - 1;
                if (links.Count < 1)
                {
                    Console.WriteLine("Not found.");
                }

                var prs = new ProcessStartInfo("chrome.exe");
                if(linkNum>=0)
                {
                    prs.Arguments = links[linkNum];
                    Process.Start(prs);
                }
            } while (linkNum >= 0);

            CalcCosineSim();
        }
    }
}
