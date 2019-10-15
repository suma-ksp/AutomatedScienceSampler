using KerboKatz.ASS;

namespace UniversalStorage2
{
    public class ActivatorFactory : IScienceActivatorFactory
    {
        public IScienceActivator GetActivatorInstance()
        {
            return new Activator<USAdvancedScience>();
        }
    }
}