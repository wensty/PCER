using System.Collections.Generic;

namespace PotionCraftCustomerPlanner;

internal enum PlannerChildWindow
{
    Main,
    RandomRule,
    Picker
}

internal sealed class PlannerChildWindows
{
    private readonly List<PlannerChildWindow> opened = new List<PlannerChildWindow>();
    private int handledFrame = -1;

    public bool HasOpenWindows => opened.Count > 0;
    public bool WasEscapeHandled(int frame) => handledFrame == frame;

    public void Open(PlannerChildWindow window)
    {
        opened.Remove(window);
        if (window == PlannerChildWindow.Main)
            opened.Insert(0, window);
        else
            opened.Add(window);
    }

    public void Close(PlannerChildWindow window) => opened.Remove(window);

    // Update, the game's close command, and IMGUI can see the same key press.
    // Consume it again without closing another window in the same frame.
    public bool TryCloseLast(int frame, out PlannerChildWindow? window)
    {
        window = null;
        if (WasEscapeHandled(frame))
            return true;
        if (!HasOpenWindows)
            return false;

        int last = opened.Count - 1;
        window = opened[last];
        opened.RemoveAt(last);
        handledFrame = frame;
        return true;
    }
}
