using System;
using YAFC.UI;

namespace YAFC.Model
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