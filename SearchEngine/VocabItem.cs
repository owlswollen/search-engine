using System.Collections.Generic;

namespace SearchEngine
{
    class VocabItem
    {
        public string word;
        public int count;
        public int tumYerler;
        public Dictionary<string, List<List<int>>> pageInfo = new Dictionary<string, List<List<int>>>();

        public VocabItem(string word, string uniName)
        {
            this.word = word;
            this.count = 0;
            List<List<int>> locationsList = new List<List<int>>();
            pageInfo.Add(uniName, locationsList);
        }
        public void AddIndexes(string uniName, int pageNumber, List<int> indexesInPage)
        {
            count++;
            tumYerler += indexesInPage.Count;
            List<int> locations = new List<int>();
            locations.Add(pageNumber);
            locations.Add(indexesInPage.Count);
            foreach (int index in indexesInPage)
            {
                locations.Add(index);
            }
            try
            {
                pageInfo[uniName].Add(locations);
            }
            catch
            {
                List<List<int>> locationsList = new List<List<int>>();
                pageInfo.Add(uniName, locationsList);
                pageInfo[uniName].Add(locations);
            }
        }
    }
}
