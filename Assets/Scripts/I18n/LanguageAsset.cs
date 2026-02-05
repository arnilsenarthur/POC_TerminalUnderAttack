using UnityEngine;

namespace TUA.I18n
{
    public class LanguageAsset : ScriptableObject
    {
        public string languageKey;
        public string languageName;
        [TextArea(10, 50)]
        public string content;
    }
}
