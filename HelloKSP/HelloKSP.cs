using UnityEngine;

namespace HelloKSP
{
    // KSPAddon makes KSP instantiate this class automatically when the main menu loads.
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class HelloKSP : MonoBehaviour
    {
        public void Start()
        {
            Debug.Log("[HelloKSP] Hello from my first KSP plugin!");
        }
    }
}