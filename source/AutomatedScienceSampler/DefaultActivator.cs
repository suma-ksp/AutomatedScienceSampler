using System;

namespace KerboKatz.ASS
{
    using System.Collections.Generic;

    internal class DefaultActivator : GenericDefaultActivator<ModuleScienceExperiment>
    {
        private static readonly Dictionary<Type, IScienceActivator> DefaultActivators = new Dictionary<Type, IScienceActivator>();

        internal static IScienceActivator GetDefaultScienceActivator(Type type, AutomatedScienceSampler ass)
        {
            if (!DefaultActivators.TryGetValue(type, out var result))
            {
                result = (IScienceActivator)Activator.CreateInstance(typeof(GenericDefaultActivator<>).MakeGenericType(type));
                result.AutomatedScienceSampler = ass;
                DefaultActivators.Add(type, result);
            }
            return result;
        }
    }
}