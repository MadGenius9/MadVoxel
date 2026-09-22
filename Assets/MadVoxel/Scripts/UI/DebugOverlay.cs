using MadVoxel.Core;
using MadVoxel.Core.Player;
using MadVoxel.World.Terrain;
using UnityEngine;
using UnityEngine.UI;

namespace MadVoxel.UI
{
    /// <summary>F3 readout: framerate, position, chunk load state and the targeted block.</summary>
    public class DebugOverlay : MonoBehaviour
    {
        Canvas _canvas;
        Text _text;
        PlayerRig _player;
        TerrainWorld _voxels;
        ChunkStreamer _streamer;
        int _seed;
        bool _developerTools;

        float _fpsAccumulator;
        int _fpsFrames;
        float _fps;

        public void Init(PlayerRig player, TerrainWorld voxels, ChunkStreamer streamer, int seed, bool developerTools)
        {
            _developerTools = developerTools;
            _player = player;
            _voxels = voxels;
            _streamer = streamer;
            _seed = seed;

            _canvas = UIKit.CreateCanvas("Debug", 20, transform);
            var panel = UIKit.Image(_canvas.transform, "Panel", new Color(0f, 0f, 0f, 0.55f));
            UIKit.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -60f), new Vector2(560f, developerTools ? 268f : 210f));

            _text = UIKit.Label(panel.transform, "Text", "", 20, TextAnchor.UpperLeft, UIKit.TextMain);
            UIKit.Stretch(_text.rectTransform, 12f);

            _canvas.enabled = false;
        }

        public void Toggle()
        {
            if (_canvas != null) _canvas.enabled = !_canvas.enabled;
        }

        void Update()
        {
            if (_canvas == null || !_canvas.enabled || _player == null) return;

            _fpsAccumulator += Time.unscaledDeltaTime;
            _fpsFrames++;
            if (_fpsAccumulator >= 0.5f)
            {
                _fps = _fpsFrames / _fpsAccumulator;
                _fpsAccumulator = 0f;
                _fpsFrames = 0;
            }

            var p = _player.transform.position;
            var chunk = ChunkCoord.FromWorld(p);

            string targetName = "-";
            var target = _player.Interaction != null ? _player.Interaction.Target : default(InteractionTarget);
            if (target.Kind == TargetKind.Block)
            {
                var def = _voxels.GetBlockDef(target.BlockCell.x, target.BlockCell.y, target.BlockCell.z);
                targetName = def != null ? def.displayName + " " + target.BlockCell : "-";
            }
            else if (target.Kind == TargetKind.Structure && target.Structure != null)
            {
                targetName = target.Structure.Definition.displayName;
            }
            else if (target.Kind == TargetKind.Entity)
            {
                targetName = "entity";
            }

            _text.text = string.Format(
                "MadVoxel debug (F3)\n" +
                "FPS {0:0}\n" +
                "Seed {1}\n" +
                "Pos {2:0.0} {3:0.0} {4:0.0}\n" +
                "Chunk {5}\n" +
                "Chunks loaded {6}   meshed {7}   jobs {8}\n" +
                "Target {9}{10}",
                _fps, _seed, p.x, p.y, p.z, chunk,
                _voxels.LoadedChunkCount,
                _streamer != null ? _streamer.VisibleChunks : 0,
                _streamer != null ? _streamer.PendingJobs : 0,
                targetName,
                _developerTools ? "\n\n" + MadVoxel.Core.DeveloperTools.KeyHelp : "");
        }
    }
}
