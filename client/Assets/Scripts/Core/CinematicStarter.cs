using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Kicks off the opening cinematic one frame after the world is built.
    ///
    /// Playing it in the same frame would fight the world builder for the
    /// camera, because the camera rig attaches the camera to the player during
    /// construction and the cinematic immediately takes it over.
    /// </summary>
    public class CinematicStarter : MonoBehaviour
    {
        private CinematicPlayer _cinematic;
        private bool _started;

        public static CinematicStarter Ensure(CinematicPlayer cinematic)
        {
            var go = new GameObject("CinematicStarter");
            var starter = go.AddComponent<CinematicStarter>();
            starter._cinematic = cinematic;
            return starter;
        }

        private void Update()
        {
            if (_started) return;
            _started = true;
            if (_cinematic != null) _cinematic.PlayAll();
        }
    }
}