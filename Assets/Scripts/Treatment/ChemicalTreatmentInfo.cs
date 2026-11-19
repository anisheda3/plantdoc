using System;
using UnityEngine;

namespace PlantDoctor.Treatment
{
    /// <summary>
    /// Chemical guidance is represented as DATA, never hard-coded advice.
    /// All fields are educational; the UI always shows the disclaimer alongside them.
    /// </summary>
    [Serializable]
    public class ChemicalTreatmentInfo
    {
        public bool involvesChemicalProduct;
        public string productOrActiveIngredient;
        public string dosage;
        public string applicationMethod;
        [TextArea] public string safetyNotes;
        [TextArea] public string localRegulationsNote =
            "Product availability and legal use vary by region. Follow the product label and local agricultural regulations.";
        public string preHarvestInterval;
    }
}
