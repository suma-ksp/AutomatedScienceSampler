using System;
using System.Collections.Generic;
using System.Reflection;
using KerboKatz;
using KerboKatz.ASS;
using KerboKatz.ReflectionWrapper;

namespace UniversalStorage2
{
    internal class Activator<T> : GenericDefaultActivator<T>
        where T : ModuleScienceExperiment
    {
        /// <inheritdoc />
        public override float GetScienceValue(ModuleScienceExperimentWrapper<T> baseExperimentWrapper, Dictionary<string, int> shipCotainsExperiments, ScienceSubject currentScienceSubject)
        {
            var scienceExperiment = ResearchAndDevelopment.GetExperiment(baseExperimentWrapper.BaseObject.experimentID);
            Log($"{nameof(GetScienceValue)} => changed from '{baseExperimentWrapper.BaseObject.experiment?.id ?? "null"}' to '{scienceExperiment.id}')");
            var result = Utilities.Science.GetScienceValue(shipCotainsExperiments, scienceExperiment, currentScienceSubject);
            Log($"{nameof(GetScienceValue)} results in {result}");
            return result;
        }

        /// <inheritdoc />
        public override void DeployExperiment(ModuleScienceExperimentWrapper<T> baseExperimentWrapper)
        {
            var baseExperiment = baseExperimentWrapper as ModuleScienceExperimentWrapper<USAdvancedScience>;
            Log($"{nameof(DeployExperiment)} => {nameof(USAdvancedScience.GatherScienceData)}({Silent})");
            baseExperiment.BaseObject.GatherScienceData(Silent);
        }

        /// <inheritdoc />
        public override ScienceSubject GetScienceSubject(ModuleScienceExperimentWrapper<T> baseExperimentWrapper)
        {
            var scienceExperiment = ResearchAndDevelopment.GetExperiment(baseExperimentWrapper.BaseObject.experimentID);
            Log($"{nameof(GetScienceSubject)} => changed from '{baseExperimentWrapper.BaseObject.experiment?.id ?? "null"}' to '{scienceExperiment.id}')");
            var currentBiome = CurrentBiome(baseExperimentWrapper.experiment);
            var result = ResearchAndDevelopment.GetExperimentSubject(scienceExperiment, ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel), FlightGlobals.currentMainBody, currentBiome, ScienceUtil.GetBiomedisplayName(FlightGlobals.currentMainBody, currentBiome));
            Log($"{nameof(GetScienceSubject)} results in '{result.id}' / '{result.title}'");
            return result;
        }

        public override void Transfer(ModuleScienceExperimentWrapper<T> baseExperimentWrapper, IScienceDataContainer moduleScienceContainer)
        {
            var baseExperiment = (baseExperimentWrapper as ModuleScienceExperimentWrapper<USAdvancedScience>).BaseObject;
            var before = baseExperimentWrapper.GetScienceCount();
            base.Transfer(baseExperimentWrapper, moduleScienceContainer);
            var mid = baseExperimentWrapper.GetScienceCount();
            foreach (var one in baseExperimentWrapper.GetData())
                baseExperiment.DumpData(one);
            var after = baseExperimentWrapper.GetScienceCount();
            Log($"Transfer ScienceCount Before / AfterTransmit / AfterDump: {before} / {mid} / {after}");
            if(after == 0)
                baseExperiment.ResetExperiment();
        }

        /// <inheritdoc />
        public override bool CanRunExperiment(ModuleScienceExperimentWrapper<T> baseExperimentWrapper, float currentScienceValue)
        {
            if (!base.CanRunExperiment(baseExperimentWrapper, currentScienceValue))
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
                var result = (bool)m.Invoke(baseExperimentWrapper.BaseObject, new object[] { Silent });
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

        public override void Reset(ModuleScienceExperimentWrapper<T> baseExperiment)
        {
            base.Reset(baseExperiment);
        }
    }
}