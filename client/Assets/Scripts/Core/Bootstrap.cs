using Megame.Core;
using UnityEngine;

namespace Megame.Client
{
    /// <summary>
    /// Creates the game root at runtime. Using a runtime hook instead of a
    /// serialized scene reference keeps the boot scene free of script GUID
    /// coupling, which matters because .meta GUIDs are generated on first
    /// import and would otherwise differ between machines.
    /// </summary>
    public static class Bootstrap
    {
        private const string GameManagerTypeName = "Megame.Client.GameManager";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateGameManager()
        {
            if (Object.FindObjectOfType<GameManager>() != null)
            {
                return;
            }

            GameObject go = new GameObject("GameManager");
            Object.DontDestroyOnLoad(go);

            // AddComponent by type would hard-link GameManager into every
            // build; resolving the type keeps the bootstrap resilient.
            System.Type type = System.Type.GetType($"{GameManagerTypeName}, Assembly-CSharp");
            if (type == null)
            {
                Debug.LogError($"Bootstrap could not resolve {GameManagerTypeName}");
                return;
            }

            go.AddComponent(type);
        }
    }
}