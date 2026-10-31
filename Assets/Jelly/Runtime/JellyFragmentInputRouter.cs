using UnityEngine;

namespace Jelly
{
    public class JellyFragmentInputRouter : MonoBehaviour
    {
        public JellyInputAdapter primaryInput;
        public JellyFragmentRegistry registry;
        public int selectedFragmentIndex = -1;

        private void FixedUpdate() => RouteInput();

        public void RouteInput()
        {
            if (primaryInput == null || registry == null) return;
            JellyInputFrame frame = primaryInput.Current;
            var list = registry.ActiveFragments;
            for (int i = 0; i < list.Count; i++)
            {
                var frag = list[i];
                if (frag == null || frag.Controller == null) continue;
                var adapter = frag.Controller.inputAdapter;
                if (adapter == null || adapter == primaryInput) continue;

                bool active = selectedFragmentIndex < 0 || selectedFragmentIndex == i;
                if (active)
                    adapter.SetSimulatedInput(frame.Move, frame.Brake, false, false);
                else
                    adapter.SetSimulatedInput(Vector2.zero, true, false, false);
            }
        }
    }
}
