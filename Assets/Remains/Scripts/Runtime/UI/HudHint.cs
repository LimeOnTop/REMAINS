namespace Remains
{
    /// <summary>
    /// Interaction hint shown under the crosshair. Systems publish a hint every frame from Update;
    /// the HUD shows the highest-priority one in LateUpdate and clears it for the next frame.
    /// </summary>
    public static class HudHint
    {
        public static string Text { get; private set; } = string.Empty;
        public static float Progress { get; private set; }
        private static int s_Priority = int.MinValue;

        public static void Set(string text, float progress = 0f, int priority = 0)
        {
            if (priority < s_Priority) return;
            s_Priority = priority;
            Text = text;
            Progress = progress;
        }

        public static void Clear()
        {
            s_Priority = int.MinValue;
            Text = string.Empty;
            Progress = 0f;
        }
    }
}
