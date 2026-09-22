using MadVoxel.Core;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// Placeholder geometry for the snap set, built from boxes in cell-local space
    /// (0..3 on X and Z, 0 upward on Y). Silhouettes matter more than detail here: a
    /// doorway has to read as a doorway across a dark field at 50 m.
    /// </summary>
    public static class BuildPieceVisuals
    {
        public const string DoorHingeName = "DoorHinge";
        public const string HatchHingeName = "HatchHinge";

        const float Cell = BuildGrid.CellSize;
        const float Level = BuildGrid.LevelHeight;

        public static void Build(BuildPiece piece)
        {
            var def = piece.Definition;
            var body = new GameObject("Body");
            body.transform.SetParent(piece.transform, false);

            var mat = MaterialLibrary.Get(def.surfaceFamily, def.tint, def.smoothness, def.metallic);
            var trim = MaterialLibrary.Get(def.surfaceFamily, def.tint * 0.72f, def.smoothness, def.metallic);

            switch (def.kind)
            {
                case BuildPieceKind.Foundation: Foundation(body.transform, mat, trim, def); break;
                case BuildPieceKind.Floor: Floor(body.transform, mat, trim); break;
                case BuildPieceKind.Wall: Wall(body.transform, mat, trim, piece.Address.Side, Level); break;
                case BuildPieceKind.HalfWall: Wall(body.transform, mat, trim, piece.Address.Side, Level * 0.5f); break;
                case BuildPieceKind.WindowWall: WindowWall(body.transform, mat, trim, piece.Address.Side); break;
                case BuildPieceKind.Doorway: Doorway(body.transform, mat, trim, piece.Address.Side); break;
                case BuildPieceKind.Door: Door(body.transform, mat, trim, piece.Address.Side); break;
                case BuildPieceKind.Stairs: Stairs(body.transform, mat, trim); break;
                case BuildPieceKind.Roof: Roof(body.transform, mat, trim); break;
                case BuildPieceKind.Ladder: Ladder(body.transform, mat, piece.Address.Side); break;
                case BuildPieceKind.Hatch: Hatch(body.transform, mat, trim); break;
                case BuildPieceKind.Fence: Fence(body.transform, mat, trim, piece.Address.Side); break;
            }

            AddCollider(piece);
        }

        // ------------------------------------------------------------- floor slots

        static void Foundation(Transform parent, Material mat, Material trim, BuildPieceDefinition def)
        {
            // The slab's top face sits exactly on the level, so a wall placed here starts flush.
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, -0.25f, Cell * 0.5f), new Vector3(Cell, 0.5f, Cell), mat, "Slab");

            // Skirt posts make a foundation read as built rather than as terrain.
            float inset = 0.22f;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? inset : Cell - inset;
                float z = (i < 2) ? inset : Cell - inset;
                PrimitiveBuilder.Box(parent, new Vector3(x, -0.55f, z), new Vector3(0.34f, 0.7f, 0.34f), trim, "Footing" + i);
            }
        }

        static void Floor(Transform parent, Material mat, Material trim)
        {
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, -0.15f, Cell * 0.5f), new Vector3(Cell, 0.3f, Cell), mat, "Deck");
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, -0.34f, Cell * 0.5f), new Vector3(Cell * 0.94f, 0.12f, 0.26f), trim, "JoistA");
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, -0.34f, Cell * 0.5f), new Vector3(0.26f, 0.12f, Cell * 0.94f), trim, "JoistB");
        }

        static void Hatch(Transform parent, Material mat, Material trim)
        {
            // Frame stays; the hinge child is what swings open.
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, -0.15f, 0.3f), new Vector3(Cell, 0.3f, 0.6f), trim, "FrameN");
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, -0.15f, Cell - 0.3f), new Vector3(Cell, 0.3f, 0.6f), trim, "FrameS");
            PrimitiveBuilder.Box(parent, new Vector3(0.3f, -0.15f, Cell * 0.5f), new Vector3(0.6f, 0.3f, Cell - 1.2f), trim, "FrameW");
            PrimitiveBuilder.Box(parent, new Vector3(Cell - 0.3f, -0.15f, Cell * 0.5f), new Vector3(0.6f, 0.3f, Cell - 1.2f), trim, "FrameE");

            var hinge = new GameObject(HatchHingeName);
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = new Vector3(0.6f, -0.15f, Cell * 0.5f);
            PrimitiveBuilder.Box(hinge.transform, new Vector3(0.9f, 0f, 0f), new Vector3(1.8f, 0.24f, Cell - 1.2f), mat, "Lid");
        }

        // -------------------------------------------------------------- wall slots

        /// <summary>Local transform that puts a panel on the given canonical edge.</summary>
        static void EdgeFrame(int side, out Vector3 centre, out Vector3 along, out Vector3 through)
        {
            // Only the north (-Z) and west (-X) edges exist after canonicalisation.
            if (side == BuildGrid.SideWest)
            {
                centre = new Vector3(0.1f, 0f, Cell * 0.5f);
                along = new Vector3(0f, 0f, 1f);
                through = new Vector3(1f, 0f, 0f);
            }
            else
            {
                centre = new Vector3(Cell * 0.5f, 0f, 0.1f);
                along = new Vector3(1f, 0f, 0f);
                through = new Vector3(0f, 0f, 1f);
            }
        }

        static Vector3 PanelSize(Vector3 along, Vector3 through, float length, float height, float thickness)
        {
            return new Vector3(
                Mathf.Abs(along.x) * length + Mathf.Abs(through.x) * thickness,
                height,
                Mathf.Abs(along.z) * length + Mathf.Abs(through.z) * thickness);
        }

        static void Wall(Transform parent, Material mat, Material trim, int side, float height)
        {
            Vector3 centre, along, through;
            EdgeFrame(side, out centre, out along, out through);

            PrimitiveBuilder.Box(parent, centre + Vector3.up * (height * 0.5f),
                PanelSize(along, through, Cell, height, 0.2f), mat, "Panel");

            // Corner posts read the tier at distance.
            PrimitiveBuilder.Box(parent, centre + along * (-Cell * 0.5f + 0.15f) + Vector3.up * (height * 0.5f),
                PanelSize(along, through, 0.3f, height, 0.28f), trim, "PostA");
            PrimitiveBuilder.Box(parent, centre + along * (Cell * 0.5f - 0.15f) + Vector3.up * (height * 0.5f),
                PanelSize(along, through, 0.3f, height, 0.28f), trim, "PostB");
        }

        static void WindowWall(Transform parent, Material mat, Material trim, int side)
        {
            Vector3 centre, along, through;
            EdgeFrame(side, out centre, out along, out through);

            PrimitiveBuilder.Box(parent, centre + Vector3.up * 0.5f, PanelSize(along, through, Cell, 1.0f, 0.2f), mat, "Sill");
            PrimitiveBuilder.Box(parent, centre + Vector3.up * 2.65f, PanelSize(along, through, Cell, 0.7f, 0.2f), mat, "Head");
            PrimitiveBuilder.Box(parent, centre + along * (-Cell * 0.5f + 0.35f) + Vector3.up * 1.65f,
                PanelSize(along, through, 0.7f, 1.6f, 0.2f), mat, "JambA");
            PrimitiveBuilder.Box(parent, centre + along * (Cell * 0.5f - 0.35f) + Vector3.up * 1.65f,
                PanelSize(along, through, 0.7f, 1.6f, 0.2f), mat, "JambB");
            PrimitiveBuilder.Box(parent, centre + Vector3.up * 1.65f, PanelSize(along, through, 0.12f, 1.55f, 0.24f), trim, "Mullion");
        }

        static void Doorway(Transform parent, Material mat, Material trim, int side)
        {
            Vector3 centre, along, through;
            EdgeFrame(side, out centre, out along, out through);

            const float opening = 1.25f;
            float jamb = (Cell - opening) * 0.5f;

            PrimitiveBuilder.Box(parent, centre + along * (-(Cell - jamb) * 0.5f) + Vector3.up * 1.1f,
                PanelSize(along, through, jamb, 2.2f, 0.2f), mat, "JambA");
            PrimitiveBuilder.Box(parent, centre + along * ((Cell - jamb) * 0.5f) + Vector3.up * 1.1f,
                PanelSize(along, through, jamb, 2.2f, 0.2f), mat, "JambB");
            PrimitiveBuilder.Box(parent, centre + Vector3.up * 2.6f, PanelSize(along, through, Cell, 0.8f, 0.2f), mat, "Header");
            PrimitiveBuilder.Box(parent, centre + Vector3.up * 2.2f, PanelSize(along, through, opening, 0.12f, 0.26f), trim, "Lintel");
        }

        static void Door(Transform parent, Material mat, Material trim, int side)
        {
            Vector3 centre, along, through;
            EdgeFrame(side, out centre, out along, out through);

            const float opening = 1.2f;

            var hinge = new GameObject(DoorHingeName);
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = centre + along * (-opening * 0.5f);

            // The panel hangs off the hinge so rotating the hinge swings the door.
            var panelOffset = along * (opening * 0.5f) + Vector3.up * 1.05f;
            PrimitiveBuilder.Box(hinge.transform, panelOffset, PanelSize(along, through, opening, 2.1f, 0.14f), mat, "Panel");
            PrimitiveBuilder.Box(hinge.transform, panelOffset + along * (opening * 0.35f) + through * 0.1f,
                new Vector3(0.12f, 0.12f, 0.12f),
                MaterialLibrary.Get(SurfaceFamily.Metal, new Color(0.33f, 0.30f, 0.27f), 0.4f, 0.8f), "Handle");
            PrimitiveBuilder.Box(hinge.transform, panelOffset + Vector3.up * 0.62f,
                PanelSize(along, through, opening * 0.92f, 0.14f, 0.18f), trim, "BandTop");
            PrimitiveBuilder.Box(hinge.transform, panelOffset - Vector3.up * 0.62f,
                PanelSize(along, through, opening * 0.92f, 0.14f, 0.18f), trim, "BandBottom");
        }

        static void Fence(Transform parent, Material mat, Material trim, int side)
        {
            Vector3 centre, along, through;
            EdgeFrame(side, out centre, out along, out through);

            const float height = 1.25f;
            PrimitiveBuilder.Box(parent, centre + along * (-Cell * 0.5f + 0.1f) + Vector3.up * (height * 0.5f),
                PanelSize(along, through, 0.2f, height, 0.2f), trim, "PostA");
            PrimitiveBuilder.Box(parent, centre + Vector3.up * (height * 0.5f),
                PanelSize(along, through, 0.2f, height, 0.2f), trim, "PostMid");
            PrimitiveBuilder.Box(parent, centre + along * (Cell * 0.5f - 0.1f) + Vector3.up * (height * 0.5f),
                PanelSize(along, through, 0.2f, height, 0.2f), trim, "PostB");

            PrimitiveBuilder.Box(parent, centre + Vector3.up * (height - 0.15f), PanelSize(along, through, Cell, 0.12f, 0.1f), mat, "RailTop");
            PrimitiveBuilder.Box(parent, centre + Vector3.up * (height * 0.5f), PanelSize(along, through, Cell, 0.1f, 0.1f), mat, "RailMid");
        }

        static void Ladder(Transform parent, Material mat, int side)
        {
            Vector3 centre, along, through;
            EdgeFrame(side, out centre, out along, out through);

            // Hang on the inside face of the edge.
            Vector3 face = centre + through * 0.22f;
            PrimitiveBuilder.Box(parent, face + along * -0.35f + Vector3.up * (Level * 0.5f),
                PanelSize(along, through, 0.12f, Level, 0.12f), mat, "RailA");
            PrimitiveBuilder.Box(parent, face + along * 0.35f + Vector3.up * (Level * 0.5f),
                PanelSize(along, through, 0.12f, Level, 0.12f), mat, "RailB");

            for (int i = 0; i < 9; i++)
            {
                PrimitiveBuilder.Box(parent, face + Vector3.up * (0.2f + i * 0.32f),
                    PanelSize(along, through, 0.8f, 0.08f, 0.08f), mat, "Rung" + i);
            }
        }

        // ---------------------------------------------------------- interior slots

        static void Stairs(Transform parent, Material mat, Material trim)
        {
            const int steps = 8;
            float rise = Level / steps;
            float run = Cell / steps;

            for (int i = 0; i < steps; i++)
            {
                PrimitiveBuilder.Box(parent,
                    new Vector3(Cell * 0.5f, rise * (i + 0.5f), run * (i + 0.5f)),
                    new Vector3(Cell - 0.2f, rise, run),
                    mat, "Step" + i);
            }

            PrimitiveBuilder.Box(parent, new Vector3(0.12f, Level * 0.5f, Cell * 0.5f), new Vector3(0.2f, Level, Cell), trim, "StringerA");
            PrimitiveBuilder.Box(parent, new Vector3(Cell - 0.12f, Level * 0.5f, Cell * 0.5f), new Vector3(0.2f, Level, Cell), trim, "StringerB");
        }

        static void Roof(Transform parent, Material mat, Material trim)
        {
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, Level + 0.15f, Cell * 0.5f), new Vector3(Cell, 0.3f, Cell), mat, "Deck");
            PrimitiveBuilder.Box(parent, new Vector3(Cell * 0.5f, Level + 0.36f, Cell * 0.5f), new Vector3(Cell * 0.96f, 0.12f, 0.3f), trim, "Ridge");
        }

        // -------------------------------------------------------------- colliders

        static void AddCollider(BuildPiece piece)
        {
            var def = piece.Definition;
            var box = piece.gameObject.AddComponent<BoxCollider>();

            switch (def.slot)
            {
                case BuildSlot.Floor:
                    box.center = new Vector3(Cell * 0.5f, -0.2f, Cell * 0.5f);
                    box.size = new Vector3(Cell, 0.4f, Cell);
                    break;

                case BuildSlot.Ceiling:
                    box.center = new Vector3(Cell * 0.5f, Level + 0.15f, Cell * 0.5f);
                    box.size = new Vector3(Cell, 0.3f, Cell);
                    break;

                case BuildSlot.Interior:
                    // Stairs need a walkable ramp; a box would be a wall, so use a mesh-free
                    // slope approximation made of the step boxes' own bounds.
                    box.center = new Vector3(Cell * 0.5f, Level * 0.5f, Cell * 0.5f);
                    box.size = new Vector3(Cell, Level, Cell);
                    box.isTrigger = true; // the steps carry the real collision
                    AddStepColliders(piece);
                    break;

                case BuildSlot.Attachment:
                    box.isTrigger = true;
                    SizeEdgeCollider(box, piece.Address.Side, Level, 0.4f);
                    break;

                default: // Wall
                    float height = Level;
                    if (def.kind == BuildPieceKind.HalfWall) height = Level * 0.5f;
                    else if (def.kind == BuildPieceKind.Fence) height = 1.25f;
                    SizeEdgeCollider(box, piece.Address.Side, height, 0.24f);
                    // A doorway must be walk-through; its posts are decoration only.
                    if (!def.blocksMovement) box.isTrigger = true;
                    break;
            }
        }

        static void SizeEdgeCollider(BoxCollider box, int side, float height, float thickness)
        {
            if (side == BuildGrid.SideWest)
            {
                box.center = new Vector3(0.1f, height * 0.5f, Cell * 0.5f);
                box.size = new Vector3(thickness, height, Cell);
            }
            else
            {
                box.center = new Vector3(Cell * 0.5f, height * 0.5f, 0.1f);
                box.size = new Vector3(Cell, height, thickness);
            }
        }

        /// <summary>Gives each stair tread its own collider so the player can walk up.</summary>
        static void AddStepColliders(BuildPiece piece)
        {
            const int steps = 8;
            float rise = Level / steps;
            float run = Cell / steps;

            for (int i = 0; i < steps; i++)
            {
                var go = new GameObject("StepCollider" + i);
                go.transform.SetParent(piece.transform, false);
                var collider = go.AddComponent<BoxCollider>();
                collider.center = new Vector3(Cell * 0.5f, rise * (i + 0.5f), run * (i + 0.5f));
                collider.size = new Vector3(Cell - 0.2f, rise, run);
            }
        }

        /// <summary>Darkens a piece as it loses health so base integrity is readable.</summary>
        public static void ShowDamage(BuildPiece piece)
        {
            float f = Mathf.Lerp(0.34f, 1f, piece.HealthFraction);
            var renderers = piece.GetComponentsInChildren<MeshRenderer>();
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", piece.Definition.tint * f);
                block.SetColor("_Color", piece.Definition.tint * f);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
