using KerboKatz;
using KerboKatz.ASS;
using KerboKatz.ReflectionWrapper;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UniversalStorage2
{
    public class Activator : GenericDefaultActivator<USAdvancedScience>
    {
        /// <inheritdoc />
        public override float GetScienceValue(ModuleScienceExperimentWrapper<USAdvancedScience> baseExperiment, Dictionary<string, int> shipCotainsExperiments, ScienceSubject currentScienceSubject)
        {
            var scienceExperiment = ResearchAndDevelopment.GetExperiment(baseExperiment.BaseObject.experimentID);
            Log($"{nameof(GetScienceValue)} => changed from '{baseExperiment.BaseObject.experiment?.id ?? "null"}' to '{scienceExperiment.id}')");
            var result = Utilities.Science.GetScienceValue(shipCotainsExperiments, scienceExperiment, currentScienceSubject);
            Log($"{nameof(GetScienceValue)} results in {result}");
            return result;
        }

        /// <inheritdoc />
        public override void DeployExperiment(ModuleScienceExperimentWrapper<USAdvancedScience> baseExperiment)
        {
            Log($"{nameof(DeployExperiment)} => {nameof(USAdvancedScience.GatherScienceData)}({Silent})");
            baseExperiment.BaseObject.GatherScienceData(Silent);
        }

        /// <inheritdoc />
        public override ScienceSubject GetScienceSubject(ModuleScienceExperimentWrapper<USAdvancedScience> baseExperiment)
        {
            var scienceExperiment = ResearchAndDevelopment.GetExperiment(baseExperiment.BaseObject.experimentID);
            Log($"{nameof(GetScienceSubject)} => changed from '{baseExperiment.BaseObject.experiment?.id ?? "null"}' to '{scienceExperiment.id}')");
            var currentBiome = CurrentBiome(baseExperiment.experiment);
            var result = ResearchAndDevelopment.GetExperimentSubject(scienceExperiment, ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel), FlightGlobals.currentMainBody, currentBiome, ScienceUtil.GetBiomedisplayName(FlightGlobals.currentMainBody, currentBiome));
            Log($"{nameof(GetScienceSubject)} results in '{result.id}' / '{result.title}'");
            return result;
        }

        /// <inheritdoc />
        public override bool CanRunExperiment(ModuleScienceExperimentWrapper<USAdvancedScience> baseExperiment, float currentScienceValue)
        {
            if (!base.CanRunExperiment(baseExperiment, currentScienceValue))
            {
                Log($"{nameof(CanRunExperiment)} => base.CanRunExperiment results in false");
                return false;
            }

            try
            {
                var m = typeof(USAdvancedScience).GetMethod("CanConduct", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (m == null)
                    throw new InvalidOperationException("Method CanConduct not found");
                if (m.ReturnType != typeof(bool))
                    throw new InvalidOperationException("Method CanConduct is not of type bool");
                if (m.GetParameters().Length != 1)
                    throw new InvalidOperationException($"Method CanConduct has wrong parameterCount ({m.GetParameters().Length})");
                if (m.GetParameters()[0].ParameterType != typeof(bool))
                    throw new InvalidOperationException($"Method CanConduct has wrong parameterType ({m.GetParameters()[0].ParameterType.FullName})");
                var result = (bool)m.Invoke(baseExperiment.BaseObject, new object[] { Silent });
                Log($"{nameof(CanRunExperiment)} => CanConduct results {result}");
                return result;
            }
            catch (Exception e)
            {
                Log($"{nameof(CanRunExperiment)} => CanConduct results in exception => true");
                Log(e);
                return true;
            }
        }
    }
}