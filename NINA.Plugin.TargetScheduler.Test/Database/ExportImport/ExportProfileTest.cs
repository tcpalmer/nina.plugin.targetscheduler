using Newtonsoft.Json;
using NINA.Plugin.TargetScheduler.Database.Schema;
using NUnit.Framework;

namespace NINA.Plugin.TargetScheduler.Test.Database.ExportImport {
    [TestFixture]
    public class ExportProfileTest {
        [Test]
        public void ExportProfile() {
            Project obj = new Project("abcd-1234");

            var jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented);
            TestContext.WriteLine(jsonString);

            Project p = JsonConvert.DeserializeObject<Project>(jsonString);
            TestContext.WriteLine($"deser:\n{p}");
        }
    }
}