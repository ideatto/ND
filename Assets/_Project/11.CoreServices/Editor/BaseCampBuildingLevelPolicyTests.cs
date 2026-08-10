using System.Collections.Generic;
using ND.Framework;
using NUnit.Framework;

namespace ND.Framework.Editor.Tests
{
    public sealed class BaseCampBuildingLevelPolicyTests
    {
        [TestCase(0, 1, false)]
        [TestCase(1, 1, true)]
        [TestCase(1, 2, false)]
        [TestCase(3, 3, true)]
        public void OrdinaryBuilding_TargetLevelCannotExceedBaseCamp(
            int baseCampLevel,
            int targetLevel,
            bool expected)
        {
            List<VillageBuildingSaveData> buildings = Buildings(baseCampLevel);

            bool result = BaseCampBuildingLevelPolicy.CanAdvance(
                "Warehouse",
                targetLevel,
                buildings,
                out int actualBaseCampLevel);

            Assert.That(result, Is.EqualTo(expected));
            Assert.That(actualBaseCampLevel, Is.EqualTo(baseCampLevel));
        }

        [Test]
        public void BaseCamp_UpgradeIsExemptFromItsOwnLevelGate()
        {
            bool result = BaseCampBuildingLevelPolicy.CanAdvance(
                BaseCampBuildingLevelPolicy.BaseCampBuildingId,
                1,
                Buildings(0),
                out _);

            Assert.That(result, Is.True);
        }

        [TestCase(0, 1, false)]
        [TestCase(4, 1, false)]
        [TestCase(5, 1, true)]
        [TestCase(5, 2, false)]
        public void EndingItem_IsSingleLevelAndRequiresBaseCampLevelFive(
            int baseCampLevel,
            int targetLevel,
            bool expected)
        {
            bool result = BaseCampBuildingLevelPolicy.CanAdvance(
                BaseCampBuildingLevelPolicy.EndingBuildingId,
                targetLevel,
                Buildings(baseCampLevel),
                out int actualBaseCampLevel);

            Assert.That(result, Is.EqualTo(expected));
            Assert.That(actualBaseCampLevel, Is.EqualTo(baseCampLevel));
            Assert.That(
                BaseCampBuildingLevelPolicy.GetRequiredBaseCampLevel(
                    BaseCampBuildingLevelPolicy.EndingBuildingId,
                    targetLevel),
                Is.EqualTo(BaseCampProgressionPolicy.EndingBuildingUnlockLevel));
        }

        [Test]
        public void DuplicateBaseCampEntries_FailClosed()
        {
            List<VillageBuildingSaveData> buildings = Buildings(1);
            buildings.Add(new VillageBuildingSaveData
            {
                displayName = BaseCampBuildingLevelPolicy.BaseCampDisplayName,
                level = 1
            });

            bool result = BaseCampBuildingLevelPolicy.CanAdvance(
                "Farm",
                1,
                buildings,
                out _);

            Assert.That(result, Is.False);
        }

        private static List<VillageBuildingSaveData> Buildings(int baseCampLevel)
        {
            var result = new List<VillageBuildingSaveData>();
            if (baseCampLevel > 0)
            {
                result.Add(new VillageBuildingSaveData
                {
                    displayName = BaseCampBuildingLevelPolicy.BaseCampDisplayName,
                    level = baseCampLevel
                });
            }
            return result;
        }
    }
}
