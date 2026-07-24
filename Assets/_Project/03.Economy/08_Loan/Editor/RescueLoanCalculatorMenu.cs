using UnityEditor;
using UnityEngine;

namespace ND.Economy.Editor
{
    public static class RescueLoanCalculatorMenu
    {
        [MenuItem("ND/Economy/Run Rescue Loan Checks")]
        public static void RunAllChecks()
        {
            try
            {
                RescueLoanCalculatorTests.RunAll();
                Debug.Log("[Rescue Loan Checks] Success");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Rescue Loan Checks] Failed: " + exception.Message);
                throw;
            }
        }
    }
}
