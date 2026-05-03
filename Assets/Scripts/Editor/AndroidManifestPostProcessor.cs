using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace BuildABot.Editor
{
    /// <summary>
    /// Safely modifies the generated AndroidManifest.xml during the build process.
    /// This avoids merge conflicts that occur with a manual AndroidManifest.xml in Unity 6.
    /// </summary>
    public class AndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // The manifest is located in src/main/AndroidManifest.xml relative to the project path
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("AndroidManifestPostProcessor: Could not find manifest at " + manifestPath);
                return;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(manifestPath);

            XmlNamespaceManager nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");

            XmlNode applicationNode = doc.SelectSingleNode("/manifest/application", nsManager);
            if (applicationNode != null)
            {
                // 1. Enable Hardware Acceleration for the whole app
                SetAttribute(doc, applicationNode, "android", "hardwareAccelerated", "true");
                
                // 2. Enable Cleartext Traffic for local StreamingAssets local loads
                SetAttribute(doc, applicationNode, "android", "usesCleartextTraffic", "true");

                // 3. Ensure Hardware Acceleration is also on the Activities
                XmlNodeList activityNodes = applicationNode.SelectNodes("activity", nsManager);
                foreach (XmlNode activity in activityNodes)
                {
                    SetAttribute(doc, activity, "android", "hardwareAccelerated", "true");
                }
                
                doc.Save(manifestPath);
                Debug.Log("<b><color=#2EA3FF>BuildABot</color></b>: Successfully injected WebView optimizations into AndroidManifest.");
            }
        }

        private void SetAttribute(XmlDocument doc, XmlNode node, string prefix, string localName, string value)
        {
            string ns = "http://schemas.android.com/apk/res/android";
            XmlElement element = (XmlElement)node;
            
            if (element.HasAttribute(localName, ns))
            {
                element.SetAttribute(localName, ns, value);
            }
            else
            {
                XmlAttribute attr = doc.CreateAttribute(prefix, localName, ns);
                attr.Value = value;
                element.Attributes.Append(attr);
            }
        }
    }
}
