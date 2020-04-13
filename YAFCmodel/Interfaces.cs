using System;
using UI;

namespace FactorioData
{
    public interface IIconAndName
    {
        Icon icon { get; }
        string name { get; }
    }

    public interface IDelayedDispatcher
    {
        void DispatchInMainThread(Action action);
    }
}