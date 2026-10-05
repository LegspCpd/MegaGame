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
        private const string GameManagerTypeName = "Megame.Core.GameManager";
        // GameManager lives in Megame.Client.asmdef, not Assembly-CSharp.
        private const string GameManagerAssemblyName = "Megame.Client";

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
            System.Type type = System.Type.GetType(
                $"{GameManagerTypeName}, {GameManagerAssemblyName}");
            if (type == null)
            {
                // Fall back to a scan: asmdef/namespace changes should not
                // leave the game booting to an empty scene.
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(GameManagerTypeName);
                    if (type != null) break;
                }
            }
            if (type == null)
            {
                Debug.LogError($"Bootstrap could not resolve {GameManagerTypeName}");
                return;
            }

            go.AddComponent(type);
        }
    }
}