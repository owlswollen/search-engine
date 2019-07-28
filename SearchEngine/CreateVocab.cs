using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Nuve.Tokenizers;
using Nuve.Lang;
using Nuve.Morphologic.Structure;
using System.Linq;

namespace SearchEngine
{
    class CreateVocab
    {
        private List<VocabItem> vocabulary = new List<VocabItem>();
        private List<int> uniPageCounts = new List<int>(); // gerek kalmayacak
        private static int pageCount = 0;
        private static string[] uniNames = { "deu", "ege", "iyte", "yasar", "ieu", "ikc", "idu" };
        //private static string[] uniNames = { "deuornek", "egeornek", "yasarornek" };
        private List<int>[] pageIndexes = new List<int>[uniNames.Length];

        public CreateVocab()
        {
            for (int i = 0; i < uniNames.Length; i++)
            {
                pageIndexes[i] = new List<int>();
            }
            Create();
            WriteVocab();
            WriteRawFreq();
            WriteUniRawFreq();
            CalcAndWriteTfIdf();
            WriteUniTfIdf();
        }

        private void Create()
        {
            for (int k = 0; k < uniNames.Length; k++)
            {
                int uniPageCounter = 0;

                foreach (string filePath in Directory.GetFiles(@"C:\Users\Gokce\Desktop\" + uniNames[k]))
                {
                    int pageIndex = Convert.ToInt32(Path.GetFileName(filePath).Split('.')[0]);
                    pageCount++;
                    uniPageCounter++;
                    pageIndexes[k].Add(Convert.ToInt32(Path.GetFileName(filePath).Split('.')[0]));

                    // Sayfalar okundu ve kucuk harfe cevrildi.
                    byte[] byteArray = File.ReadAllBytes(filePath);
                    string page = Encoding.UTF8.GetString(byteArray);
                    page = page.ToLower();

                    // Kelimeler birbirinden ayrildi.
                    IList<string> tokens = StandartSplitter.Split(page);

                    // Kelimeler kok haline getirildi.
                    List<string> stems = new List<string>();
                    Language tr = LanguageFactory.Create(LanguageType.Turkish);
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

                    // Vocabulary - inverted index olusturuldu.
                    bool found = false;
                    
                    for (int i = 0; i < stems.Count; i++)
                    {
                        found = false;
                        for (int j = 0; j < i; j++)
                        {
                            if (stems[j] == stems[i])
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            if (vocabulary.Count != 0)
                            {
                                foreach (VocabItem item in vocabulary)
                                {
                                    if (item.word.Equals(stems[i]))
                                    {
                                        found = true;
                                        List<int> indexes = Enumerable.Range(0, stems.Count).Where(x => stems[x] == stems[i]).ToList();
                                        item.AddIndexes(uniNames[k], pageIndex, indexes); //
                                        break;
                                    }
                                }
                            }
                            if (!found)
                            {
                                VocabItem myVocabItem = new VocabItem(stems[i], uniNames[k]);
                                List<int> indexes = Enumerable.Range(0, stems.Count).Where(x => stems[x] == stems[i]).ToList();
                                myVocabItem.AddIndexes(uniNames[k], pageIndex, indexes);
                                vocabulary.Add(myVocabItem);
                            }
                        }
                    }
                }
                uniPageCounts.Add(uniPageCounter);
            }
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

        private void WriteVocab()
        {
            // Vocabulary dosyaya yazildi.
            FileStream file = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "Vocabulary.txt", FileMode.OpenOrCreate, FileAccess.Write);
            StreamWriter writer = new StreamWriter(file);
            foreach (VocabItem item in vocabulary)
            {
                writer.Write(item.word + ":" + item.count + ";");
                foreach (KeyValuePair<string, List<List<int>>> pageInfo in item.pageInfo)
                {
                    writer.Write(pageInfo.Key.ToString() + " ");

                    foreach (List<int> locations in pageInfo.Value)
                    {
                        foreach (int location in locations)
                        {
                            writer.Write(location + " ");
                        }
                        writer.Write("\t");
                    }
                }
                if (vocabulary.IndexOf(item) != vocabulary.Count - 1)
                {
                    writer.WriteLine();
                }
            }
            writer.Close();
            file.Close();
        }

        // Vocabulary icerisindeki verilerden yararlanilarak rapor dosyalari olusturuldu.
        private int maxWordFreq;
        private void WriteRawFreq()
        {
            // Ham frekanslar dosyaya yazildi.
            var csv = new StringBuilder();
            var orderedVocab = vocabulary.OrderByDescending(x => x.tumYerler);
            maxWordFreq = ((VocabItem)orderedVocab.First()).tumYerler;
            foreach (VocabItem item in orderedVocab)
            {
                var first = item.word;
                var second = item.tumYerler;
                var newLine = string.Format("{0},{1}", first, second);
                csv.AppendLine(newLine);
            }
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "RawFrequencies.csv", csv.ToString());
        }

        Dictionary<string, int[]> uniRawFreqDict; // kelime, unilerin tum sayfalarindaki total frekanslar
                                                  //SILint[] maxFreqInUniPages; // her uni icin sirali olarak o universitenin tum sayfalari icindeki max frekans

        class MaxOthers
        {
            public Dictionary<int, int> maxFreqs;
            public Dictionary<int, int> nextFreqs;
            public Dictionary<int, string> words;
        }
        Dictionary<string, MaxOthers> maxOthersFreq = new Dictionary<string, MaxOthers>();
        private void WriteUniRawFreq()
        {
            // Sozcuklerin herbir universitenin kendi sayfalari dahilindeki ham frekanslari bir array'de tutuldu.
            // Herbir sozcuk ve o sozcuge karsilik gelen array bir dictionary'de tutuldu.
            uniRawFreqDict = new Dictionary<string, int[]>();
            //SILmaxFreqInUniPages = new int[uniNames.Length];
            int freqInUniPagesSum = 0;
            foreach (VocabItem item in vocabulary)
            {
                int[] freqsInUniPages = new int[uniNames.Length];

                for (int i = 0; i < uniNames.Length; i++)
                {
                    freqInUniPagesSum = 0;
                    try
                    {
                        foreach (List<int> locations in item.pageInfo[uniNames[i]])
                        {
                            freqInUniPagesSum += locations[1];
                        }
                        freqsInUniPages[i] = freqInUniPagesSum;
                    }
                    catch
                    {
                        freqsInUniPages[i] = 0;
                    }
                }
                uniRawFreqDict.Add(item.word, freqsInUniPages);
            }

            DirectoryInfo di = Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "RawFreqsInUniPages");

            // Dictionary'deki sozcukler ve array'deki frekanslari sirayla array'in ilgili elemanina gore artmayan sekilde siralandi.
            // Universite sayfalari dahilindeki ham frekanslar dosyaya yazildi.
            for (int i = 0; i < uniNames.Length; i++)
            {
                var csv = new StringBuilder();
                var orderedUniRawFreqDict = uniRawFreqDict.OrderByDescending(x => x.Value[i]);
                //SILmaxFreqInUniPages[i] = (orderedUniRawFreqDict.First()).Value[i];
                foreach (KeyValuePair<string, int[]> rawFreqs in orderedUniRawFreqDict)
                {
                    if (rawFreqs.Value[i] != 0)
                    {
                        var first = rawFreqs.Key.ToString();
                        var second = rawFreqs.Value[i];
                        var newLine = string.Format("{0},{1}", first, second);
                        csv.AppendLine(newLine);
                    }
                }
                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "RawFreqsInUniPages\\" + uniNames[i] + "RawFrequencies.csv", csv.ToString());
            }

            // tum uniler icin ayri klasor olusturuldu
            // herbirinin icindeki sayfalar ozelinde raw frequency'ler ayri dosyalara yazdirildi
            for (int i = 0; i < uniNames.Length; i++)
            {
                DirectoryInfo di1 = Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "RawFreqsInUniPages\\" + uniNames[i]);

                MaxOthers mo = new MaxOthers();
                mo.maxFreqs = new Dictionary<int, int>();
                mo.nextFreqs = new Dictionary<int, int>();
                mo.words = new Dictionary<int, string>();
                foreach (int pageIndex in pageIndexes[i])
                {
                    Dictionary<string, int> rawFreqsinPage = new Dictionary<string, int>();
                    foreach (VocabItem item in vocabulary)
                    {
                        try
                        {
                            foreach (List<int> list in item.pageInfo[uniNames[i]])
                            {
                                if (list[0] == pageIndex)
                                {
                                    rawFreqsinPage.Add(item.word, list[1]);
                                }
                            }
                        }
                        catch { }
                    }
                    var orderedRawFreqs = rawFreqsinPage.OrderByDescending(x => x.Value);

                    
                    int k = 0;
                    var csv = new StringBuilder();
                    foreach (KeyValuePair<string, int> item in orderedRawFreqs)
                    {
                        if (k == 0)
                        {
                            mo.maxFreqs.Add(pageIndex, item.Value);
                            mo.words.Add(pageIndex, item.Key);
                        }
                        if (k == 1)
                        {
                            mo.nextFreqs.Add(pageIndex, item.Value);
                        }
                        k++;
                        var first = item.Key.ToString();
                        var second = item.Value;
                        var newLine = string.Format("{0},{1}", first, second);
                        csv.AppendLine(newLine);
                    }
                    
                    File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "RawFreqsInUniPages\\" + uniNames[i] + "\\" + pageIndex + ".csv", csv.ToString());
                }
                maxOthersFreq.Add(uniNames[i], mo);
            }
        }

        class TfIdfClass
        {
            public string word;
            public string uniName;
            public int pageIndex;
            public double tfIdf;
            public double uniTfIdf;
        }
        List<TfIdfClass>[] tfIdfs = new List<TfIdfClass>[uniNames.Length];
        private void CalcAndWriteTfIdf()
        {
            for (int i = 0; i < uniNames.Length; i++)
            {
                tfIdfs[i] = new List<TfIdfClass>();
            }

            // sozcugun bir uni dahilinde kac sayfada gectiginin hesabi
            // her uni icin sirasiyla bi array'e yerlestirildi
            // sozcuk, array dictionary'si tutuldu
            Dictionary<string, int[]> uniPageCountDict = new Dictionary<string, int[]>(); // !!!isim degisecek - bir sozcugun bir universite dahilinde kac sayfada gectigi (tum universiteler icin sirasiyla)
            foreach (VocabItem item in vocabulary)
            {
                int[] uniPagesCounts = new int[uniNames.Length]; // !!!isim degisecek

                for (int i = 0; i < uniNames.Length; i++)
                {
                    try
                    {
                        uniPagesCounts[i] = item.pageInfo[uniNames[i]].Count;
                    }
                    catch
                    {
                        uniPagesCounts[i] = 0;
                    }
                }
                uniPageCountDict.Add(item.word, uniPagesCounts);
            }

            // tf-idf degerleri hesaplandi.
            // sozcuk, universite adi, sayfa indisi ve tf-idf degerleri universitelerin listelerine eklendi.
            TfIdfClass tfIdfItem;
            foreach (VocabItem item in vocabulary)
            {
                int sumPageCount = 0;
                foreach (int count in uniPageCountDict[item.word])
                {
                    sumPageCount += count;
                }

                for (int i = 0; i < uniNames.Length; i++)
                {
                    try
                    {
                        foreach (List<int> locations in item.pageInfo[uniNames[i]])
                        {
                            double tfIdf;
                            double uniTfIdf;
                            if (item.word != maxOthersFreq[uniNames[i]].words[locations[0]])
                            {
                                tfIdf = (0.5 + 0.5 * ((double)locations[1] / (double)maxOthersFreq[uniNames[i]].maxFreqs[locations[0]])) * Math.Log(((double)pageCount / (double)sumPageCount), 10);
                                uniTfIdf = (0.5 + 0.5 * ((double)locations[1] / (double)maxOthersFreq[uniNames[i]].maxFreqs[locations[0]])) * Math.Log(((double)uniPageCounts[i] / (double)uniPageCountDict[item.word][i]), 10);
                            }
                            else
                            {
                                tfIdf = (0.5 + 0.5 * ((double)locations[1] / (double)maxOthersFreq[uniNames[i]].nextFreqs[locations[0]])) * Math.Log(((double)pageCount / (double)sumPageCount), 10);
                                uniTfIdf = (0.5 + 0.5 * ((double)locations[1] / (double)maxOthersFreq[uniNames[i]].nextFreqs[locations[0]])) * Math.Log(((double)uniPageCounts[i] / (double)uniPageCountDict[item.word][i]), 10);
                            }

                            tfIdfItem = new TfIdfClass();
                            tfIdfItem.uniName = uniNames[i];
                            tfIdfItem.word = item.word;
                            tfIdfItem.pageIndex = locations[0];
                            tfIdfItem.tfIdf = tfIdf;
                            tfIdfItem.uniTfIdf = uniTfIdf;
                            tfIdfs[i].Add(tfIdfItem);
                        }
                    }
                    catch
                    {
                        // Kelime bir sayfada hic gecmiyorsa o sayfa icin tf-idf hesaplanmadi.
                    }
                }
            }

            for (int i = 0; i < uniNames.Length; i++)
            {
                DirectoryInfo di = Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "TF-IDFs");

                foreach (int pageIndex in pageIndexes[i])
                {
                    List<TfIdfClass> tfIdfsInPage = new List<TfIdfClass>();
                    foreach (TfIdfClass item in tfIdfs[i])
                    {
                        if (item.pageIndex == pageIndex)
                        {
                            tfIdfsInPage.Add(item);
                        }
                    }
                    var orderedTfIdfs = tfIdfsInPage.OrderByDescending(x => x.tfIdf);

                    var csv = new StringBuilder();
                    foreach (TfIdfClass item in orderedTfIdfs)
                    {
                        var first = item.word;
                        var second = item.tfIdf;
                        var newLine = string.Format("{0},{1}", first, second);
                        csv.AppendLine(newLine);
                    }
                    File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "TF-IDFs\\" + uniNames[i] + pageIndex + ".csv", csv.ToString());
                }
            }
        }

        private void WriteUniTfIdf()
        {
            // her uni icin bir klasor acildi.
            // unilerdeki her sayfa icin ayri dosya olusururlup tf-idf'ler sirali sekilde yazdirildi.
            DirectoryInfo di = Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "TF-IDFsInUniPages");
            for (int i = 0; i < uniNames.Length; i++)
            {
                DirectoryInfo di1 = Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "TF-IDFsInUniPages\\" + uniNames[i]);

                foreach (int pageIndex in pageIndexes[i])
                {
                    List<TfIdfClass> tfIdfsInPage = new List<TfIdfClass>();
                    foreach (TfIdfClass item in tfIdfs[i])
                    {
                        if (item.pageIndex == pageIndex)
                        {
                            tfIdfsInPage.Add(item);
                        }
                    }
                    var orderedTfIdfs = tfIdfsInPage.OrderByDescending(x => x.uniTfIdf);

                    var csv = new StringBuilder();
                    foreach (TfIdfClass item in orderedTfIdfs)
                    {
                        var first = item.word;
                        var second = item.uniTfIdf;
                        var newLine = string.Format("{0},{1}", first, second);
                        csv.AppendLine(newLine);
                    }
                    File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "TF-IDFsInUniPages\\" + uniNames[i] + "\\" + pageIndex + ".csv", csv.ToString());
                }
            }
        }
    }
}
