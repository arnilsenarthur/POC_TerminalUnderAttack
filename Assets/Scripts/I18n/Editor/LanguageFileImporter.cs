using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace TUA.I18n.Editor
{
    [ScriptedImporter(1, "lang")]
    public class LanguageFileImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var fileContent = File.ReadAllText(ctx.assetPath);
            var fileName = Path.GetFileNameWithoutExtension(ctx.assetPath);
            var languageName = _ExtractLanguageName(fileContent);
            var languageAsset = ScriptableObject.CreateInstance<LanguageAsset>();
            languageAsset.languageKey = fileName;
            languageAsset.languageName = !string.IsNullOrEmpty(languageName) ? languageName : fileName;
            languageAsset.content = fileContent;
            languageAsset.name = fileName;
            ctx.AddObjectToAsset("main", languageAsset);
            ctx.SetMainObject(languageAsset);
        }
        
        private string _ExtractLanguageName(string content)
        {
            if (string.IsNullOrEmpty(content))
                return null;
            
            using var reader = new StringReader(content);
            var firstLine = reader.ReadLine();
            
            if (string.IsNullOrEmpty(firstLine))
                return null;
            
            return firstLine.StartsWith("name=") ? firstLine.Substring(5).Trim() : null;
        }
    }
}
