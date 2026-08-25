using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GCodeGenerator.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GCodeGenerator.Persistence;

namespace GCodeGenerator.Tests
{
    /// <summary>
    /// Проверяет изолированную границу совместимости Metadata: форматы v1-v3
    /// мигрируются в типизированные свойства, а доменная модель и формат v4
    /// больше не содержат универсального словаря.
    /// </summary>
    [TestClass]
    public class LegacyMetadataMigrationTests
    {
        private static ProjectFileService Service { get; } = new ProjectFileService();

        private static string ReferenceOutputDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reference");

        [TestMethod]
        public void LegacyV1_DrillOperations_MigrateToTypedProperties()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(path).Operations;

            Assert.IsNotNull(loaded);
            Assert.AreEqual(19, loaded.Count);

            var expectedModes = new[]
            {
                DrillMode.Points, DrillMode.Line, DrillMode.Array, DrillMode.Rect,
                DrillMode.Circle, DrillMode.Arc, DrillMode.Polygon, DrillMode.Ellipse,
                DrillMode.Package
            };
            CollectionAssert.AreEqual(
                expectedModes,
                loaded.Take(9).Cast<DrillPointsOperation>().Select(op => op.DrillMode).ToArray());

            var line = (DrillPointsOperation)loaded[1];
            Assert.AreEqual(10.0, line.StartX, 1e-9);
            Assert.AreEqual(5.0, line.Distance, 1e-9);
            Assert.AreEqual(5, line.HoleCount);

            var rectangle = (DrillPointsOperation)loaded[3];
            Assert.AreEqual(10, rectangle.Holes.Count);

            var ellipse = (DrillPointsOperation)loaded[7];
            Assert.AreEqual(25.0, ellipse.RadiusX, 1e-9);
            Assert.AreEqual(15.0, ellipse.RadiusY, 1e-9);

            var package = (DrillPointsOperation)loaded[8];
            Assert.AreEqual("SOIC-8", package.PackageName);
        }

        [TestMethod]
        public void LegacyV1_OpenSave_ProducesV4WithoutMetadata()
        {
            var path = Path.Combine(ReferenceOutputDirectory, "legacy_project_v1.ygc");
            var loaded = Service.Load(path).Operations;

            var json = Service.Serialize(loaded, null);

            StringAssert.StartsWith(json, "{\"version\":4,\"operations\":[");
            Assert.IsFalse(json.Contains("\"Metadata\"", StringComparison.Ordinal));

            var reloaded = Service.Deserialize(json).Operations;
            Assert.AreEqual(19, reloaded.Count);
            CollectionAssert.AreEqual(
                loaded.Select(op => op.GetType()).ToArray(),
                reloaded.Select(op => op.GetType()).ToArray());
            CollectionAssert.AreEqual(
                loaded.Take(9).Cast<DrillPointsOperation>().Select(op => op.DrillMode).ToArray(),
                reloaded.Take(9).Cast<DrillPointsOperation>().Select(op => op.DrillMode).ToArray());
        }

        [TestMethod]
        public void LegacyV3_DrillMetadata_MigratesBeforeModelDeserializationCompletes()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"DrillPoints\",\"data\":{" +
                "\"Holes\":[{\"X\":10,\"Y\":20,\"TotalDepth\":2,\"StepDepth\":1}]," +
                "\"Distance\":99,\"Metadata\":{\"StartX\":10,\"StartY\":20,\"StartZ\":1," +
                "\"Distance\":5,\"HoleCount\":4,\"AngleDeg\":30,\"TotalDepth\":6," +
                "\"StepDepth\":2,\"FeedZRapid\":700,\"FeedZWork\":250,\"RetractHeight\":0.5}}}]}";

            var operation = (DrillPointsOperation)Service.Deserialize(json).Operations.Single();

            Assert.AreEqual(DrillMode.Line, operation.DrillMode);
            Assert.AreEqual(10.0, operation.StartX, 1e-9);
            Assert.AreEqual(5.0, operation.Distance, 1e-9, "Metadata старого формата имеет прежний приоритет");
            Assert.AreEqual(4, operation.HoleCount);
            Assert.AreEqual(6.0, operation.TotalDepth, 1e-9);
        }

        [TestMethod]
        public void LegacyV3_PocketMetadata_MigratesBeforeSave()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"PocketCircle\",\"data\":{" +
                "\"Radius\":5,\"ToolDiameter\":3,\"Metadata\":{\"Radius\":12,\"CenterX\":4," +
                "\"CenterY\":6,\"ToolDiameter\":8,\"Direction\":1,\"PocketStrategy\":0," +
                "\"Decimals\":2,\"IsRoughingEnabled\":true}}}]}";

            var operation = (PocketCircleOperation)Service.Deserialize(json).Operations.Single();

            Assert.AreEqual(12.0, operation.Radius, 1e-9);
            Assert.AreEqual(8.0, operation.ToolDiameter, 1e-9);
            Assert.AreEqual(MillingDirection.CounterClockwise, operation.Direction);
            Assert.AreEqual(PocketStrategy.Concentric, operation.PocketStrategy);
            Assert.AreEqual(2, operation.Decimals);
            Assert.IsTrue(operation.IsRoughingEnabled);
            Assert.IsFalse(Service.Serialize(new OperationBase[] { operation }, null)
                .Contains("\"Metadata\"", StringComparison.Ordinal));
        }

        [TestMethod]
        public void LegacyProfile_MetadataRemainsNonCanonical()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"ProfileCircle\",\"data\":{" +
                "\"CenterX\":1,\"Radius\":20,\"Metadata\":{\"Radius\":999,\"CustomKey\":42}}}]}";

            var operation = (ProfileCircleOperation)Service.Deserialize(json).Operations.Single();

            Assert.AreEqual(20.0, operation.Radius, 1e-9);
            Assert.IsFalse(Service.Serialize(new OperationBase[] { operation }, null)
                .Contains("\"Metadata\"", StringComparison.Ordinal));
        }

        [TestMethod]
        public void LegacyMetadata_UnknownKeysRejectLoadInsteadOfBeingLost()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"DrillPoints\",\"data\":{" +
                "\"Metadata\":{\"CustomKey\":42}}}]}";

            var exception = Assert.ThrowsException<NotSupportedException>(() => Service.Deserialize(json));
            StringAssert.Contains(exception.Message, "CustomKey");
            StringAssert.Contains(exception.Message, "не потерялись");
        }

        [TestMethod]
        public void CurrentV4_RejectsMetadataEvenWhenEmpty()
        {
            const string json = "{\"version\":4,\"operations\":[{\"type\":\"PocketCircle\",\"data\":{" +
                "\"Radius\":10,\"Metadata\":{}}}]}";

            var exception = Assert.ThrowsException<NotSupportedException>(() => Service.Deserialize(json));
            StringAssert.Contains(exception.Message, "не поддерживается форматом v4");
        }

        [TestMethod]
        public void DomainOperationTypes_DoNotExposeMetadataProperty()
        {
            var operationTypes = new[]
            {
                typeof(DrillPointsOperation),
                typeof(PocketCircleOperation), typeof(PocketRectangleOperation),
                typeof(PocketEllipseOperation), typeof(PocketDxfOperation),
                typeof(ProfileCircleOperation), typeof(ProfileRectangleOperation),
                typeof(ProfileRoundedRectangleOperation), typeof(ProfileEllipseOperation),
                typeof(ProfilePolygonOperation), typeof(ProfileDxfOperation)
            };

            foreach (var operationType in operationTypes)
            {
                Assert.IsNull(
                    operationType.GetProperty("Metadata", BindingFlags.Instance | BindingFlags.Public),
                    operationType.Name);
            }
        }

        [TestMethod]
        public void Migrator_DistinguishesArrayAndRectangleByGeneratedHoleCount()
        {
            var array = CreateDrillWithHoles(12);
            var rectangle = CreateDrillWithHoles(10);
            var arrayMetadata = GridMetadata();
            var rectangleMetadata = GridMetadata();

            LegacyMetadataMigrator.Migrate(array, arrayMetadata);
            LegacyMetadataMigrator.Migrate(rectangle, rectangleMetadata);

            Assert.AreEqual(DrillMode.Array, array.DrillMode);
            Assert.AreEqual(DrillMode.Rect, rectangle.DrillMode);
            Assert.AreEqual(0, arrayMetadata.Count);
            Assert.AreEqual(0, rectangleMetadata.Count);
        }

        [TestMethod]
        public void LegacyV2_PocketRectangleMetadata_MigratesGeometry()
        {
            const string json = "{\"version\":2,\"operations\":[{\"type\":\"PocketRectangle\",\"data\":{"
                + "\"Width\":1,\"Height\":2,\"Metadata\":{\"Width\":40,\"Height\":20,\"RotationAngle\":30,"
                + "\"ReferencePointType\":1}}}]}";

            var operation = (PocketRectangleOperation)Service.Deserialize(json).Operations.Single();

            Assert.AreEqual(40.0, operation.Width, 1e-9);
            Assert.AreEqual(20.0, operation.Height, 1e-9);
            Assert.AreEqual(30.0, operation.RotationAngle, 1e-9);
            Assert.AreEqual(ReferencePointType.TopLeft, operation.ReferencePointType);
        }

        [TestMethod]
        public void LegacyV3_PocketEllipseMetadata_MigratesGeometry()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"PocketEllipse\",\"data\":{"
                + "\"RadiusX\":1,\"RadiusY\":2,\"Metadata\":{\"RadiusX\":15,\"RadiusY\":8,"
                + "\"CenterX\":3,\"CenterY\":4,\"RotationAngle\":45}}}]}";

            var operation = (PocketEllipseOperation)Service.Deserialize(json).Operations.Single();

            Assert.AreEqual(15.0, operation.RadiusX, 1e-9);
            Assert.AreEqual(8.0, operation.RadiusY, 1e-9);
            Assert.AreEqual(3.0, operation.CenterX, 1e-9);
            Assert.AreEqual(45.0, operation.RotationAngle, 1e-9);
        }

        [TestMethod]
        public void LegacyMetadata_EmptyObjectIsAccepted()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"PocketDxf\",\"data\":{"
                + "\"Metadata\":{}}}]}";

            Assert.IsInstanceOfType<PocketDxfOperation>(Service.Deserialize(json).Operations.Single());
        }

        [TestMethod]
        public void LegacyMetadata_NonObjectIsRejected()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"DrillPoints\",\"data\":{"
                + "\"Metadata\":42}}]}";

            Assert.ThrowsException<System.Text.Json.JsonException>(() => Service.Deserialize(json));
        }

        [TestMethod]
        public void LegacyMetadata_DuplicateFieldIsRejected()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"DrillPoints\",\"data\":{"
                + "\"Metadata\":{},\"Metadata\":{}}}]}";

            Assert.ThrowsException<System.Text.Json.JsonException>(() => Service.Deserialize(json));
        }

        [TestMethod]
        public void LegacyPocketDxf_UnknownMetadataIsRejected()
        {
            const string json = "{\"version\":3,\"operations\":[{\"type\":\"PocketDxf\",\"data\":{"
                + "\"Metadata\":{\"Radius\":999}}}]}";

            var exception = Assert.ThrowsException<NotSupportedException>(() => Service.Deserialize(json));
            StringAssert.Contains(exception.Message, "Radius");
        }

        private static DrillPointsOperation CreateDrillWithHoles(int count)
        {
            var operation = new DrillPointsOperation();
            for (var i = 0; i < count; i++)
                operation.Holes.Add(new DrillHole());
            return operation;
        }

        private static Dictionary<string, object> GridMetadata()
        {
            return new Dictionary<string, object>
            {
                ["StartX"] = 0,
                ["StartY"] = 0,
                ["StartZ"] = 0,
                ["Distance"] = 5,
                ["HoleCount"] = 4,
                ["AngleDeg"] = 0,
                ["RowPitch"] = 5,
                ["RowCount"] = 3
            };
        }
    }
}
