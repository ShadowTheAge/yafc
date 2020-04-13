using System;
using System.Collections.Generic;

namespace FactorioData
{
    public static class EnvironmentSettings
    {
        public static HashSet<string> allMods;
        public static IDelayedDispatcher dispatcher;
        public static Random random = new Random();
    }
}