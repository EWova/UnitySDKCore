using System;
using System.IO;

using UnityEditor;

using UnityEngine;
namespace EWova.Core.Editor
{
    [InitializeOnLoad]
    internal static class PkgVerGen
    {
        internal const string PackageJsonGuid = "7f3736fa8649e7841b760919810adde7";
        internal const string PackageInfoGuid = "8c02a6f5d904fab4b8da85773504b9ed";
        internal static void Generate()
        {
            var packagePath = AssetDatabase.GUIDToAssetPath(PackageJsonGuid);

            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError($"[PkgVerGen] Cannot resolve package.json GUID: {PackageJsonGuid}");
                return;
            }

            var infoPath = AssetDatabase.GUIDToAssetPath(PackageInfoGuid);

            if (string.IsNullOrEmpty(infoPath))
            {
                Debug.LogError($"[PkgVerGen] Cannot resolve PackageInfo.cs GUID: {PackageInfoGuid}");
                return;
            }

            if (!File.Exists(packagePath))
            {
                Debug.LogError($"[PkgVerGen] package.json not found: {packagePath}");
                return;
            }

            var json = File.ReadAllText(packagePath);
            var package = JsonUtility.FromJson<PackageJson>(json);

            var code = $@"// auto generated
namespace EWova 
{{
    internal static class PackageInfo
    {{
        public const string Version = ""{package.version}"";
    }}
}}";

            if (!File.Exists(infoPath))
            {
                var dir = Path.GetDirectoryName(infoPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            var existing = File.Exists(infoPath)
                ? File.ReadAllText(infoPath)
                : null;

            if (existing == code)
                return;

            File.WriteAllText(infoPath, code);

            AssetDatabase.Refresh();
        }

        [System.Serializable]
        private class PackageJson
        {
            public string version;
        }
    }
}
