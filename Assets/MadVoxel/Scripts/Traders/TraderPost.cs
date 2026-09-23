using MadVoxel.Core;
using MadVoxel.Core.Player;
using UnityEngine;

namespace MadVoxel.Traders
{
    /// <summary>
    /// The counter you walk up to. One of these stands at each trader outpost the world
    /// generator laid down, holding that trader's books.
    ///
    /// It is deliberately not a person. A trader NPC would need pathing, a schedule, a
    /// greeting animation and somewhere to sleep, and none of that changes what trading
    /// actually is here: a counter, a price board, and what is in your bag. The stall is
    /// the thing you look for on the horizon.
    /// </summary>
    public class TraderPost : MonoBehaviour, IInteractable
    {
        /// <summary>Raised when the player interacts. The UI layer listens.</summary>
        public static event System.Action<TraderPost> OpenRequested;

        public TraderState State { get; private set; }

        public TraderDefinition Definition { get { return State != null ? State.Definition : null; } }

        WorldClock _clock;

        public void Init(TraderDefinition definition, WorldClock clock)
        {
            _clock = clock;

            State = new TraderState();
            State.Init(definition, clock != null ? clock.TotalHours : 0.0);

            gameObject.name = "Trader_" + definition.stringId;
            TraderVisuals.Build(transform, definition);
        }

        /// <summary>
        /// Tops the shelves up to the current hour. Called when the player arrives
        /// rather than on a timer: a trader nobody is standing at does not need to be
        /// simulated, and restocking is derived from the clock so arriving late gets
        /// exactly the same answer as having been there all along.
        /// </summary>
        public void Refresh()
        {
            if (State != null && _clock != null) State.Refresh(_clock.TotalHours);
        }

        public string InteractPrompt
        {
            get
            {
                if (State == null) return "";
                return string.Format("{0} - {1}", Definition.displayName, State.TierName);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (State == null || interactor == null) return;
            if (interactor.GetComponent<PlayerRig>() == null) return;

            Refresh();
            if (OpenRequested != null) OpenRequested(this);
        }
    }

    /// <summary>
    /// The stall. A counter under a canopy with a lamp over it, because the thing that
    /// matters is recognising it from four hundred metres away across a field.
    /// </summary>
    public static class TraderVisuals
    {
        public static void Build(Transform root, TraderDefinition definition)
        {
            var timber = MaterialLibrary.Get(SurfaceFamily.Plank, new Color(0.34f, 0.26f, 0.18f), 0.1f);
            var metal = MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.42f, 0.40f, 0.38f), 0.4f, 0.7f);
            var canvasCloth = MaterialLibrary.Get(SurfaceFamily.Cloth, new Color(0.62f, 0.33f, 0.20f), 0.05f);

            PrimitiveBuilder.Box(root, new Vector3(0f, 0.5f, 0f), new Vector3(3.2f, 1.0f, 0.9f), timber, "Counter");
            PrimitiveBuilder.Box(root, new Vector3(0f, 1.06f, 0f), new Vector3(3.4f, 0.12f, 1.1f), timber, "Top");

            // Posts and a canopy, so the silhouette is a stall and not a crate.
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 1.5f;
                float z = (i < 2 ? -1f : 1f) * 0.55f;
                PrimitiveBuilder.Box(root, new Vector3(x, 1.35f, z), new Vector3(0.12f, 2.7f, 0.12f), metal, "Post" + i);
            }

            PrimitiveBuilder.Box(root, new Vector3(0f, 2.75f, 0f), new Vector3(3.6f, 0.1f, 1.6f), canvasCloth, "Canopy");
            PrimitiveBuilder.Box(root, new Vector3(0f, 2.45f, -0.8f), new Vector3(3.6f, 0.5f, 0.08f), canvasCloth, "Valance");

            // A price board on the back wall. It says nothing - the screen does that -
            // but it tells you which side of the stall to stand at.
            PrimitiveBuilder.Box(root, new Vector3(0f, 1.9f, 0.62f), new Vector3(2.4f, 1.2f, 0.06f), timber, "Board");

            var lampGo = new GameObject("StallLamp");
            lampGo.transform.SetParent(root, false);
            lampGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);

            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 12f;
            lamp.intensity = 1.4f;
            lamp.color = new Color(0.96f, 0.78f, 0.42f);
            lamp.shadows = LightShadows.None;

            var box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(3.4f, 2f, 1.2f);
        }
    }
}
