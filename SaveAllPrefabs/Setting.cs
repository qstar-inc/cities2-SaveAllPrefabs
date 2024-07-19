using System.Collections.Generic;
using Colossal;

namespace SaveAllPrefabs
{
    public class LocaleEN : IDictionarySource
    {
        public LocaleEN()
        {
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { "Editor.NETWORKCONTAINER", "Networks" },
                { "Editor.AREACONTAINER", "Areas" },
                { "Editor.OTHERCONTAINER", "Other" },
            };
        }

        public void Unload()
        {

        }
    }
}
