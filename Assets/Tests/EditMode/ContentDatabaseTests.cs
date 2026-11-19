using NUnit.Framework;
using PlantDoctor.Diseases;
using PlantDoctor.Plants;
using UnityEditor;

namespace PlantDoctor.Tests
{
    /// <summary>The content database snapshot (Resources/Content/plant_doctor_content.json) the app ships and reads.</summary>
    public class ContentDatabaseTests
    {
        [Test]
        public void EveryDiseaseInTheAppHasDatabaseContent()
        {
            Assert.IsTrue(ContentDatabase.Available, "snapshot missing: run tools/data/make_content_db.py");
            var database = AssetDatabase.LoadAssetAtPath<PlantDatabase>("Assets/Data/Plants/PlantDatabase.asset");
            int count = 0;
            foreach (var plant in database.All)
                foreach (var d in plant.diseases)
                {
                    var c = ContentDatabase.Find(plant.plantId, d.diseaseId);
                    Assert.IsNotNull(c, $"{plant.plantId}/{d.diseaseId} not in the content database");
                    Assert.IsNotEmpty(c.description, $"{plant.plantId}/{d.diseaseId} description");
                    Assert.IsNotEmpty(c.steps, $"{plant.plantId}/{d.diseaseId} steps");
                    Assert.AreEqual(c.description, d.description, $"{plant.plantId}/{d.diseaseId}: setup did not apply the database");
                    count++;
                }
            Assert.Greater(count, 100);
        }

        [Test]
        public void DatasetLabelsResolveThroughTheDatabase()
        {
            Assert.IsTrue(ContentDatabase.TryResolveLabel("Tomato___Late_blight", null, out var plant, out var disease));
            Assert.AreEqual("tomato", plant);
            Assert.IsNotNull(ContentDatabase.Find(plant, disease));
            Assert.IsFalse(ContentDatabase.TryResolveLabel("not a class", null, out _, out _));
        }
    }
}
