using System;
using UI;

namespace FactorioData
{
    public interface IIconAndName
    {
        Sprite icon { get; }
        string name { get; }
    }

    public interface IDelayedDispatcher
    {
        void DispatchInMainThread(Action action);
    }
}