using UnityEngine;

namespace DungeonGenerator
{
    /// <summary>
    /// Pauses gameplay via <see cref="Time.timeScale"/> and a static flag (input still runs in <c>Update</c>, so systems must guard on <see cref="IsPaused"/>).
    /// </summary>
    public static class GameplayPause
    {
        private static float _savedTimeScale = 1f;

        public static bool IsPaused { get; private set; }

        public static void SetPaused(bool paused)
        {
            if (paused == IsPaused)
            {
                return;
            }

            if (paused)
            {
                _savedTimeScale = Time.timeScale;
                if (_savedTimeScale <= 0f)
                {
                    _savedTimeScale = 1f;
                }

                Time.timeScale = 0f;
            }
            else
            {
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            }

            IsPaused = paused;
        }
    }
}
