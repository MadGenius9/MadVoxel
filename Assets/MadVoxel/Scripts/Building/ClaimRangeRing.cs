using MadVoxel.Core;
using MadVoxel.Core.Player;
using UnityEngine;

namespace MadVoxel.Building
{
    /// <summary>
    /// The tool cupboard's reach, drawn on the dirt as a faint rust ring while a snap
    /// piece is in hand. Claim Slate keeps this in the world rather than on the visor:
    /// the boundary is a fact about the ground, so it belongs on the ground.
    ///
    /// It follows the terrain by raycasting each segment down, so a ring across a dug
    /// trench sinks into it instead of floating over the hole. That is not free, so it
    /// only rebuilds when the player has actually moved or a claim has changed.
    /// </summary>
    public class ClaimRangeRing : MonoBehaviour
    {
        const int Segments = 72;
        const float RebuildInterval = 0.4f;
        const float RebuildDistance = 1.5f;

        /// <summary>Beyond this, the ring is not the thing you are thinking about.</summary>
        const float ShowWithin = 6f;

        StructureWorld _structures;
        PlayerRig _player;

        LineRenderer _line;
        Material _material;
        LandClaim _shown;

        Vector3 _lastBuiltAt;
        float _nextRebuild;

        public void Init(StructureWorld structures, PlayerRig player)
        {
            _structures = structures;
            _player = player;

            _material = MakeMaterial(new Color(ClaimSlateColours.RustR, ClaimSlateColours.RustG, ClaimSlateColours.RustB, 0.38f));

            _line = gameObject.AddComponent<LineRenderer>();
            _line.material = _material;
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = Segments;
            _line.widthMultiplier = 0.09f;
            _line.numCapVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            var claim = ClaimToShow();
            if (claim == null)
            {
                if (_line != null && _line.enabled) _line.enabled = false;
                _shown = null;
                return;
            }

            Vector3 eye = _player.transform.position;
            bool moved = (eye - _lastBuiltAt).sqrMagnitude > RebuildDistance * RebuildDistance;

            if (claim != _shown || (moved && Time.time >= _nextRebuild))
            {
                Rebuild(claim);
                _shown = claim;
                _lastBuiltAt = eye;
                _nextRebuild = Time.time + RebuildInterval;
            }

            if (!_line.enabled) _line.enabled = true;
        }

        /// <summary>
        /// The nearest claim whose edge is close enough to matter, and only while the
        /// player is actually building. On foot the ground stays clean.
        /// </summary>
        LandClaim ClaimToShow()
        {
            if (_structures == null || _player == null || _line == null) return null;
            if (_structures.Claims == null) return null;

            var interaction = _player.Interaction;
            if (interaction == null || !interaction.Build.Active) return null;

            var claims = _structures.Claims.Claims;
            if (claims.Count == 0) return null;

            Vector3 eye = _player.transform.position;
            LandClaim best = null;
            float bestGap = float.MaxValue;

            for (int i = 0; i < claims.Count; i++)
            {
                var claim = claims[i];
                Vector3 d = claim.Centre - eye;
                d.y = 0f;

                // Distance to the edge, not the stake: standing dead centre of a big
                // claim should not light up a ring 24 m away.
                float gap = Mathf.Abs(d.magnitude - claim.Radius);
                if (gap > ShowWithin || gap >= bestGap) continue;

                best = claim;
                bestGap = gap;
            }
            return best;
        }

        void Rebuild(LandClaim claim)
        {
            Vector3 centre = claim.Centre;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i / (float)Segments * Mathf.PI * 2f;
                float x = centre.x + Mathf.Cos(angle) * claim.Radius;
                float z = centre.z + Mathf.Sin(angle) * claim.Radius;

                _line.SetPosition(i, new Vector3(x, GroundAt(x, z, centre.y), z));
            }
        }

        /// <summary>
        /// Rides edited terrain by looking for the real surface, falling back to the
        /// claim's own height when the column is unloaded or the ray finds nothing.
        /// </summary>
        float GroundAt(float x, float z, float fallbackY)
        {
            var origin = new Vector3(x, fallbackY + 30f, z);
            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, 90f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y + 0.06f;
            }
            return fallbackY + 0.06f;
        }

        static Material MakeMaterial(Color colour)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = MaterialLibrary.LitShader;

            var mat = new Material(shader);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
            return mat;
        }
    }

    /// <summary>
    /// Claim Slate's rust, repeated as plain numbers. The world layer must not depend on
    /// the UI assembly, and this is the one colour it needs.
    /// </summary>
    static class ClaimSlateColours
    {
        public const float RustR = 0.722f; // 0xB8
        public const float RustG = 0.353f; // 0x5A
        public const float RustB = 0.196f; // 0x32
    }
}
