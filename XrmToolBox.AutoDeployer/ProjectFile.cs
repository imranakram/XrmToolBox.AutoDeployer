using System.IO;
using System.Xml.Serialization;

namespace XrmToolBox.AutoDeployer
{
    // Reads and writes .adproj files
    internal static class ProjectFile
    {
        #region Public Methods

        public static Project Load(string fileName)
        {
            var serializer = new XmlSerializer(typeof(Project));
            using (var reader = new StreamReader(fileName))
            {
                return (Project)serializer.Deserialize(reader);
            }
        }

        public static void Save(Project project, string fileName)
        {
            var serializer = new XmlSerializer(typeof(Project));
            using (var writer = new StreamWriter(fileName))
            {
                serializer.Serialize(writer, project);
            }
        }

        #endregion Public Methods
    }
}
