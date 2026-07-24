using UnityEditor;
using UnityEngine;

namespace ND.Economy.Editor
{
    public static class BuildingCostCalculatorMenu
    {
        [MenuItem("ND/Economy/Run Building Cost Checks")]
        public static void RunAllChecks()
        {
            try
            {
                BuildingCostCalculatorTests.RunAll();
                BuildingCostCatalogTests.RunAll();
                CaravanBuildingConstructionCommandTests.RunAll();
                Debug.Log("[Building Cost Checks] Success");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Building Cost Checks] Failed: " + exception.Message);
                throw;
            }
        }
    }
}
